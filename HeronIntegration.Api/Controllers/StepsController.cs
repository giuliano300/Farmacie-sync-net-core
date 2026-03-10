using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using static HeronIntegration.Engine.Persistence.Mongo.Repositories.StepRepository;

namespace HeronIntegration.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/steps")]
public class StepsController : ControllerBase
{
    private readonly IStepProcessorResolver _resolver;
    private readonly IStepRepository _stepRepo;
    private readonly ICleanupService _cleanupService;
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _runningSteps = new();

    public StepsController(
        IStepProcessorResolver resolver,
        IStepRepository stepRepo,
        ICleanupService cleanupService)
    {
        _resolver = resolver;
        _stepRepo = stepRepo;
        _cleanupService = cleanupService;
    }

    [HttpGet("{batchId}")]
    public async Task<IActionResult> GetSteps(string batchId)
    {
        var steps = await _stepRepo.GetByBatchAsync(batchId);
        return Ok(steps);
    }

    [HttpPost("run")]
    public async Task<IActionResult> RunStep(RunStepRequest req)
    {
        var step = await _stepRepo.GetStepAsync(req.BatchId, req.Step);

        if (step == null)
            throw new Exception($"Step non trovato {req.Step}");

        if (step.Status == Shared.Enums.StepStatus.Success)
            throw new Exception($"Step già lavorato {req.Step}");

        await _stepRepo.SetRunningAsync(step.Id.ToString());

        var processor = _resolver.Resolve(req.Step);

        var token = StartStepProcess(req.BatchId);

        _ = Task.Run(async () =>
        {
            try
            {
                token.ThrowIfCancellationRequested();

                var result = await processor.ExecuteAsync(req.BatchId);

                if (result.Success)
                    await _stepRepo.SetSuccessAsync(step.Id.ToString(), result.FinishedAt);
                else
                    await _stepRepo.SetErrorAsync(step.Id.ToString(), result!.ErrorMessage!);
            }
            catch (OperationCanceledException)
            {
                await _stepRepo.SetErrorAsync(step.Id.ToString(), "Step cancellato");
            }
            catch (Exception ex)
            {
                await _stepRepo.SetErrorAsync(step.Id.ToString(), ex.Message);
            }

        }, token);

        return Ok();
    }

    [HttpPost("retry")]
    public async Task<IActionResult> RetryStep(RetryStepRequest req)
    {
        // FERMA tutto quello che gira
        if (_runningSteps.TryRemove(req.BatchId, out var existing))
            existing.Cancel();

        var step = await _stepRepo.GetStepAsync(req.BatchId, req.Step);

        if (step == null)
            throw new Exception($"Step non trovato {req.Step}");

        await _stepRepo.SetRunningAsync(step.Id.ToString());

        // step successivi
        var nextSteps = StepFlow.GetNextSteps(req.Step);
        // reset solo gli step successivi
        await _cleanupService.CleanupPipeLineAsync(req.Step, req.BatchId);
        if (nextSteps.Count > 0)
        {
            await _stepRepo.ResetNextStepsAsync(req.BatchId, nextSteps);
        }

        var processor = _resolver.Resolve(req.Step);

        var token = StartStepProcess(req.BatchId);

        _ = Task.Run(async () =>
        {
            try
            {
                token.ThrowIfCancellationRequested();

                var result = await processor.ExecuteAsync(step.BatchId.ToString());

                if (result.Success)
                    await _stepRepo.SetSuccessAsync(step.Id.ToString(), result.FinishedAt);
                else
                    await _stepRepo.SetErrorAsync(step.Id.ToString(), result!.ErrorMessage!);
            }
            catch (OperationCanceledException)
            {
                await _stepRepo.SetErrorAsync(step.Id.ToString(), "Step cancellato");
            }
            catch (Exception ex)
            {
                await _stepRepo.SetErrorAsync(step.Id.ToString(), ex.Message);
            }

        }, token);

        return Ok();
    }


    private CancellationToken StartStepProcess(string batchId)
    {
        if (_runningSteps.TryRemove(batchId, out var existing))
            existing.Cancel();

        var cts = new CancellationTokenSource();
        _runningSteps[batchId] = cts;

        return cts.Token;
    }
}
