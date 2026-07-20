using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using Microsoft.AspNetCore.Mvc;

/// <summary>Resets failed export state so the Engine can retry one AIC or a batch.</summary>
[ApiController]
[Route("api/admin/export")]
public class ExportRetryController : ControllerBase
{
    private readonly IExportRepository _exportRepo;

    public ExportRetryController(IExportRepository exportRepo)
    {
        _exportRepo = exportRepo;
    }

    // Reset only: execution remains the responsibility of the Engine pipeline.
    [HttpPost("{batchId}/retry/{aic}")]
    public async Task<IActionResult> RetrySingle(string batchId, string aic)
    {
        await _exportRepo.ResetSingleAsync(batchId, aic);
        return Ok();
    }

    // retry batch intero
    [HttpPost("{batchId}/retry-all")]
    public async Task<IActionResult> RetryBatch(string batchId)
    {
        await _exportRepo.ResetBatchAsync(batchId);
        return Ok();
    }
}
