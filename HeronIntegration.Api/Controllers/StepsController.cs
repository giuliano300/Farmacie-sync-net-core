using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace HeronIntegration.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/steps")]
public class StepsController : ControllerBase
{
    private readonly IStepProcessorResolver _resolver;
    private readonly IStepRepository _stepRepo;
    private readonly ICleanupService _cleanupService;
    private readonly BatchProcessManager _processManager;

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
        BatchProcessManager processManager)
    {
        _resolver = resolver;
        _stepRepo = stepRepo;
        _cleanupService = cleanupService;
        _processManager = processManager;
    }

    [HttpGet("{batchId}")]
    public async Task<IActionResult> GetSteps(string batchId)
    {
        var steps = await _stepRepo.GetByBatchAsync(batchId);
        return Ok(steps);
    }

    // RUN singolo step
    [HttpPost("run")]
    public async Task<IActionResult> RunStep(RunStepRequest req)
    {
        var step = await ValidateStep(req.BatchId, req.Step);

        var stepId = step.Id.ToString();

        var token = _processManager.Start(ProcessType.Batch, req.BatchId);

        _ = RunBackground(async () =>
        {
            await _stepRepo.SetRunningAsync(stepId);

            var processor = _resolver.Resolve(req.Step);

            var result = await processor.ExecuteAsync(req.BatchId, token);

            await HandleResult(stepId, result);

        }, stepId, token);

        return Ok();
    }

    // RUN pipeline completa da uno step
    [HttpPost("run-pipeline")]
    public async Task<IActionResult> RunPipeline(RunStepRequest req)
    {
        var step = await ValidateStep(req.BatchId, req.Step);

        var stepId = step.Id.ToString();

        var token = _processManager.Start(ProcessType.Batch, req.BatchId);

        _ = RunBackground(async () =>
        {
            await ExecutePipeline(req.BatchId, req.Step, token);

        }, stepId, token);

        return Ok();
    }

    // RETRY pipeline
    [HttpPost("retry")]
    public async Task<IActionResult> RetryStep(RetryStepRequest req)
    {
        StopRunningBatch(req.BatchId);

        var token = _processManager.Start(ProcessType.Batch, req.BatchId);

        _ = RunBackground(async () =>
        {
            await _cleanupService.CleanupPipeLineAsync(req.Step, req.BatchId);

            await ExecutePipeline(req.BatchId, req.Step, token);

        }, null, token);

        return Ok();
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
}