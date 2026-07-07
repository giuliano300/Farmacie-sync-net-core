using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Newtonsoft.Json.Linq;

[ApiController]
[Route("api/batches-report")]
public class BatchReportController : ControllerBase
{
    private readonly IBatchReportRepository _batchRepoReport;
    private readonly IBatchRepository _batchRepo;
    private readonly IBatchManagerService _batchManagerService;
    private readonly IMagentoExporterFactory _magentoExporterFactory;
    private readonly ICustomerRepository _customerRepo;
    private readonly BatchProcessManager _processManager;
    public BatchReportController(
        IBatchReportRepository batchRepoReport,
        IBatchRepository batchRepo,
        IBatchManagerService batchManagerService,
        ICustomerRepository customerRepo,
        IMagentoExporterFactory magentoExporterFactory,
        BatchProcessManager processManager
    )
    {
        _batchRepoReport = batchRepoReport;
        _batchRepo = batchRepo;
        _batchManagerService = batchManagerService;
        _customerRepo = customerRepo;
        _magentoExporterFactory = magentoExporterFactory;
        _processManager = processManager;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string batchId)
        => Ok(await _batchRepoReport.GetBatchesAsync(batchId));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
        => Ok(await _batchRepoReport.GetByIdAsync(id));


    [HttpGet("history")]
    public async Task<PagedResult<CompleteBatchesItem>> GetHistory(
        string customerId,
        int pageIndex = 0,
        int pageSize = 10)
    {
        pageIndex = Math.Max(0, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 100);
        pageIndex = Math.Min(pageIndex, int.MaxValue / pageSize);

        var page = await _batchRepo.GetPastBatchPageByCustomerId(customerId, pageIndex, pageSize);

        return new PagedResult<CompleteBatchesItem>
        {
            Items = await BuildCompleteBatchesAsync(page.Items),
            TotalCount = page.TotalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    [HttpGet("today")]
    public async Task<List<CompleteBatchesItem>> today()
    {
        var allPast = await _batchRepo.GetAllTodayClosed();

        return await BuildCompleteBatchesAsync(allPast);
    }

    private async Task<List<CompleteBatchesItem>> BuildCompleteBatchesAsync(List<BatchExecution> batches)
    {
        var batchIds = batches
            .Select(x => x.Id.ToString())
            .ToList();

        var reportsTask = _batchRepoReport.GetBatchesByBatchIdsAsync(batchIds);
        var dashboardsTask = _batchRepo.BuildBatchDashboards(batches);

        await Task.WhenAll(reportsTask, dashboardsTask);

        var reportsByBatchId = reportsTask.Result;
        var dashboardsByBatchId = dashboardsTask.Result
            .ToDictionary(x => x.BatchId);

        return batchIds
            .Select(batchId => new CompleteBatchesItem
            {
                Batch = dashboardsByBatchId[batchId],
                Report = reportsByBatchId.GetValueOrDefault(batchId)
            })
            .ToList();
    }

    [HttpDelete("{id}")]
    public async Task<bool> Delete(string id)
    {
        try
        {
            _processManager.Start(ProcessType.Batch, id);
            var batch = await _batchRepo.GetByIdAsync(id);
            var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId);

            if (customer?.Magento == null)
                throw new Exception("Magento config mancante");

            await _batchManagerService.DeleteAsync(id);

            return true;
        }
        catch
        {
            return false;
        }
    }


}
