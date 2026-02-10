using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/admin/export")]
public class ExportRetryController : ControllerBase
{
    private readonly IExportRepository _exportRepo;

    public ExportRetryController(IExportRepository exportRepo)
    {
        _exportRepo = exportRepo;
    }

    // retry singolo prodotto
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
