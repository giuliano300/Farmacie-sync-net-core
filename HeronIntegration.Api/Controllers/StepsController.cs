using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HeronIntegration.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/steps")]
public class StepsController : ControllerBase
{
    private readonly IStepProcessorResolver _resolver;
    private readonly IStepRepository _stepRepo;

    public StepsController(
        IStepProcessorResolver resolver,
        IStepRepository stepRepo)
    {
        _resolver = resolver;
        _stepRepo = stepRepo;
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

        var processor = _resolver.Resolve(req.Step);

        var result = await processor.ExecuteAsync(req.BatchId);

        if (result.Success)
            await _stepRepo.SetSuccessAsync(step.Id.ToString(), result.StartedAt, result.FinishedAt);
        else
            await _stepRepo.SetErrorAsync(step.Id.ToString(), result!.ErrorMessage!);

        return Ok();
    }

    [HttpPost("retry")]
    public async Task<IActionResult> RetryStep(RetryStepRequest req)
    {
        await _stepRepo.ResetStepsAsync(req.BatchId);

        var step = await _stepRepo.GetStepAsync(req.BatchId, req.Step);

        if (step == null)
            throw new Exception($"Step non trovato {req.Step}");

        var processor = _resolver.Resolve(req.Step);

        await processor.ExecuteAsync(step.BatchId.ToString());

        return Ok();
    }
}
