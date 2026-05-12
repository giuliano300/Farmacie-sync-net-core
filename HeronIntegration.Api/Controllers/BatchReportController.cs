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

    [HttpGet("today")]
    public async Task<List<CompleteBatchesItem>> today()
    {
        var allPast = await _batchRepo.GetAllTodayClosed();

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
            var token = _processManager.Start(ProcessType.Batch, id);
            var batch = await _batchRepo.GetByIdAsync(id);
            var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId);

            if (customer?.Magento == null)
                throw new Exception("Magento config mancante");

            var exporter = _magentoExporterFactory.Create(customer.Magento);
            await _batchManagerService.DeleteAsync(id);
            await exporter.DeleteProducts();
            await exporter.CleanIndex(token);
            await exporter.CleanCache(token);

            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }


}
