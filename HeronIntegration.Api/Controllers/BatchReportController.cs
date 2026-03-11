using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

[ApiController]
[Route("api/batches-report")]
public class BatchReportController : ControllerBase
{
    private readonly IBatchReportRepository _batchRepoReport;
    private readonly IBatchRepository _batchRepo;
    public BatchReportController(
        IBatchReportRepository batchRepoReport,
        IBatchRepository batchRepo
        )
    {
        _batchRepoReport = batchRepoReport;
        _batchRepo = batchRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string batchId)
        => Ok(await _batchRepoReport.GetBatchesAsync(batchId));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
        => Ok(await _batchRepoReport.GetByIdAsync(id));


    [HttpGet("history")]
    public async Task<List<CompleteBatchesItem>> GetHistory(string customerId)
    {
        var allPast = await _batchRepo.GetAllPastBatchByCustomerId(customerId);

        var res = new List<CompleteBatchesItem>();

        foreach (var batch in allPast)
        {
            var result = new CompleteBatchesItem();

            var b = await _batchRepo.BuildBatchDashboard(batch);
            result.Batch = b;
            var r = await _batchRepoReport.GetBatchesAsync(batch.Id.ToString());
            result.Report = r;

            res.Add(result);
        }

        return res;
    }

    [HttpDelete("{id}")]
    public async Task<bool> Delete(string id)
    {
        try
        {
            await _batchRepo.DeleteAsync(id);
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }


}
