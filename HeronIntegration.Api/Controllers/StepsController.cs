using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Collections.Concurrent;

namespace HeronIntegration.Admin.Api.Controllers;

/// <summary>
/// Controls durable pipeline state. Endpoints reset MongoDB step state and mark the
/// batch running; BatchOrchestratorWorker performs the actual execution.
/// </summary>
[ApiController]
[Route("api/admin/steps")]
public class StepsController : ControllerBase
{
    private readonly IStepProcessorResolver _resolver;
    private readonly IStepRepository _stepRepo;
    private readonly ICleanupService _cleanupService;
    private readonly BatchProcessManager _processManager;
    private readonly IEnrichedProductRepository _enrichedRepo;
    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly IBatchRepository _batchRepo;


    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _runningSteps = new();

    private static readonly string[] OrderedSteps =
    {
        "HeronImport",
        "Farmadati",
        "Suppliers",
        "Magento"
    };

    public StepsController(
        IStepProcessorResolver resolver,
        IStepRepository stepRepo,
        ICleanupService cleanupService,
        BatchProcessManager processManager,
        IEnrichedProductRepository enrichedRepo,
        IResolvedProductRepository resolvedRepo,
        IBatchRepository batchRepo)
    {
        _resolver = resolver;
        _stepRepo = stepRepo;
        _cleanupService = cleanupService;
        _processManager = processManager;
        _enrichedRepo = enrichedRepo;
        _resolvedRepo = resolvedRepo;
        _batchRepo = batchRepo;
    }

    [HttpGet("{batchId}")]
    public async Task<IActionResult> GetSteps(string batchId)
    {
        var steps = await _stepRepo.GetByBatchAsync(batchId);
        return Ok(steps);
    }

    // Both run commands currently queue the durable pipeline from the selected step.
    [HttpPost("run")]
    public async Task<IActionResult> RunStep(RunStepRequest req)
    {
        await ValidateStep(req.BatchId, req.Step);
        await QueuePipelineAsync(req.BatchId, req.Step, null, cleanup: false);

        return Ok();
    }

    // RUN pipeline completa da uno step
    [HttpPost("run-pipeline")]
    public async Task<IActionResult> RunPipeline(RunStepRequest req)
    {
        await ValidateStep(req.BatchId, req.Step);
        await QueuePipelineAsync(req.BatchId, req.Step, null, cleanup: false);

        return Ok();
    }

    // RETRY pipeline
    [HttpPost("retry")]
    public async Task<IActionResult> RetryStep(RetryStepRequest req)
    {
        StopRunningBatch(req.BatchId);
        await QueuePipelineAsync(req.BatchId, req.Step, null, cleanup: true);

        return Ok();
    }

    [HttpPost("retryByType")]
    public async Task<IActionResult> RetryByType(RetryStepRequest req)
    {
        StopRunningBatch(req.BatchId);
        await QueuePipelineAsync(req.BatchId, req.Step, req.type, cleanup: true);

        return Ok();
    }

    private async Task QueuePipelineAsync(
        string batchId,
        string startStep,
        TypeRun? type,
        bool cleanup)
    {
        var startIndex = Array.IndexOf(OrderedSteps, startStep);
        if (startIndex < 0)
            throw new Exception($"Step non valido {startStep}");

        if (cleanup)
            await _cleanupService.CleanupPipeLineAsync(startStep, batchId);

        await _stepRepo.ResetNextStepsAsync(
            batchId,
            OrderedSteps.Skip(startIndex).ToList());

        if (type.HasValue)
            await _batchRepo.SetTypeAsync(batchId, type);

        await _batchRepo.SetRunningAsync(batchId);
    }

    // =========================
    // Helpers
    // =========================

    private async Task<StepExecution> ValidateStep(string batchId, string stepName)
    {
        var step = await _stepRepo.GetStepAsync(batchId, stepName);

        if (step == null)
            throw new Exception($"Step non trovato {stepName}");

        if (step.Status == StepStatus.Success)
            throw new Exception($"Step già lavorato {stepName}");

        return step;
    }

    private async Task HandleResult(string stepId, StepExecutionResult result)
    {
        if (result.Success)
            await _stepRepo.SetSuccessAsync(stepId, result.FinishedAt);
        else
            await _stepRepo.SetErrorAsync(stepId, result.ErrorMessage!);
    }

    private void StopRunningBatch(string batchId)
    {
        if (_runningSteps.TryRemove(batchId, out var existing))
        {
            existing.Cancel();
            existing.Dispose();
        }
    }
    private Task RunBackground(Func<Task> action, string? stepId, CancellationToken token)
    {
        return Task.Run(async () =>
        {
            try
            {
                token.ThrowIfCancellationRequested();

                await action();
            }
            catch (OperationCanceledException)
            {
                if (stepId != null)
                    await _stepRepo.SetErrorAsync(stepId, "Step cancellato");
            }
            catch (Exception ex)
            {
                if (stepId != null)
                    await _stepRepo.SetErrorAsync(stepId, ex.Message);
            }

        }, token);
    }

    private async Task ExecutePipeline(string batchId, string startStep, CancellationToken token)
    {
        var startIndex = Array.IndexOf(OrderedSteps, startStep);

        if (startIndex == -1)
            throw new Exception("Step non valido");

        for (int i = startIndex; i < OrderedSteps.Length; i++)
        {
            token.ThrowIfCancellationRequested();

            var stepName = OrderedSteps[i];

            var step = await _stepRepo.GetStepAsync(batchId, stepName);

            if (step == null)
                break;

            var stepId = step.Id.ToString();

            await _stepRepo.SetRunningAsync(stepId);

            var processor = _resolver.Resolve(stepName);

            var result = await processor.ExecuteAsync(batchId, token);

            if (result.Success)
            {
                await _stepRepo.SetSuccessAsync(stepId, result.FinishedAt);
            }
            else
            {
                await _stepRepo.SetErrorAsync(stepId, result.ErrorMessage!);
                break;
            }
        }
    }
    private async Task ExecutePipelineByType(string batchId, string startStep, TypeRun? type, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        //IMPORT HERON
        var stepName = OrderedSteps[0];

        var step = await _stepRepo.GetStepAsync(batchId, stepName);

        if (step == null)
            return;

        var stepId = step.Id.ToString();

        await _stepRepo.SetRunningAsync(stepId);

        var processor = _resolver.Resolve(stepName);

        var result = await processor.ExecuteAsync(batchId, token);

        if (result.Success)
        {
            await _stepRepo.SetSuccessAsync(stepId, result.FinishedAt);
        }
        else
        {
            await _stepRepo.SetErrorAsync(stepId, result.ErrorMessage!);
            return;
        }

        // Farmadati enrichment always runs after Heron import.
        stepName = OrderedSteps[1];

        step = await _stepRepo.GetStepAsync(batchId, stepName);

        if (step == null)
            return;

        stepId = step.Id.ToString();

        await _stepRepo.SetRunningAsync(stepId);

        processor = _resolver.Resolve(stepName);

        result = await processor.ExecuteAsync(batchId, token);

        if (result.Success)
        {
            await _stepRepo.SetSuccessAsync(stepId, result.FinishedAt);
        }
        else
        {
            await _stepRepo.SetErrorAsync(stepId, result.ErrorMessage!);
            return;
        }

        // Supplier comparison can be skipped for image-only runs.
        stepName = OrderedSteps[2];
        step = await _stepRepo.GetStepAsync(batchId, stepName);
        if (step == null)
            return;

        stepId = step.Id.ToString();

        if(type != TypeRun.ImportImmagini)
        {

            await _stepRepo.SetRunningAsync(stepId);

            processor = _resolver.Resolve(stepName);

            result = await processor.ExecuteAsync(batchId, token);

            if (result.Success)
            {
                await _stepRepo.SetSuccessAsync(stepId, result.FinishedAt);
            }
            else
            {
                await _stepRepo.SetErrorAsync(stepId, result.ErrorMessage!);
                return;
            }

        }
        else
        {
            // Image-only runs still need resolved rows, so copy Heron availability as-is.
            var batchObjectId = ObjectId.Parse(batchId);

            var raws = await _enrichedRepo.GetByBatchAsync(batchId);
            var resolvedList = new List<ResolvedProduct>(raws.Count);

            foreach (var raw in raws)
            {
                // HERON sempre candidato
                var chosen = new SupplierStock
                {
                    SupplierCode = "HERON",
                    Aic = raw.Aic,
                    Price = raw.HeronPrice,
                    Availability = raw.HeronStock
                };

                resolvedList.Add(ResolvedProduct.MapToResolved(raw, chosen, batchObjectId));
            }

            if (resolvedList.Count > 0)
                await _resolvedRepo.InsertManyAsync(resolvedList);

            await _stepRepo.SetSuccessAsync(stepId, result.FinishedAt);
        }

        // Magento receives the requested run type and completes the external export.
        stepName = OrderedSteps[3];
        step = await _stepRepo.GetStepAsync(batchId, stepName);
        if (step == null)
            return;

        stepId = step.Id.ToString();

        await _stepRepo.SetRunningAsync(stepId);

        processor = _resolver.Resolve(stepName);

        result = await processor.ExecuteAsync(batchId, token, type);

        await _stepRepo.SetSuccessAsync(stepId, result.FinishedAt);

    }
}
