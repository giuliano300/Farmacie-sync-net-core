using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.StepProcessors;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SharpCompress.Common;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IBatchRepository _batchRepo;
    private readonly IStepRepository _stepRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IRawProductRepository _rawRepo;
    private readonly IHostEnvironment _env;
    private readonly IEnrichedProductRepository _enrichedRepo;
    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly IExportRepository _exportRepo;

    public DashboardController(
        IBatchRepository batchRepo,
        IStepRepository stepRepo,
        IHostEnvironment env,
        ICustomerRepository customerRepo,
        IRawProductRepository rawRepo,
        IEnrichedProductRepository enrichedRepo,
        IResolvedProductRepository resolvedRepo,
        IExportRepository exportRepo
        )
    {
        _batchRepo = batchRepo;
        _stepRepo = stepRepo;
        _env = env;
        _customerRepo = customerRepo;
        _rawRepo = rawRepo;
        _enrichedRepo = enrichedRepo;
        _resolvedRepo = resolvedRepo;
        _exportRepo = exportRepo;
    }

    [HttpGet("")]
    public async Task<DashboardResponse> GetDashboard()
    {
        var todayBatches = await _batchRepo.GetTodayAsync();

        var result = new DashboardResponse();

        foreach (var batch in todayBatches)
        {
            var item = await BuildBatchDashboard(batch);

            if (batch.Status == BatchStatus.Running)
                result.ActiveBatches.Add(item);
            else
                result.CompletedBatches.Add(item);
        }

        return result;
    }

    private async Task<BatchDashboardItem> BuildBatchDashboard(BatchExecution batch)
    {
        var batchId = batch.Id.ToString();

        var steps = await _stepRepo.GetByBatchAsync(batchId);

        var currentStep = steps
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();

        var rawTotal = await _rawRepo.CountByBatchAsync(batchId);
        var enrichedTotal = await _enrichedRepo.CountByBatchAsync(batchId);
        var resolvedTotal = await _resolvedRepo.CountByBatchAsync(batchId);

        var export = await _exportRepo.GetByBatchAsync(batchId);
        var exportTotal = export.Count();

        var exportPending = export.Where(a => a.Status == ExportStatus.Pending).Count();
        var exportSuccess = export.Where(a => a.Status == ExportStatus.Success).Count();
        var exportInsert = export.Where(a => a.Status == ExportStatus.Insert).Count();
        var exportUpdatePrice = export.Where(a => a.Status == ExportStatus.UpdatePrice).Count();
        var exportInsertImage = export.Where(a => a.Status == ExportStatus.InsertImages).Count();
        var exportError = export.Where(a => a.Status == ExportStatus.Error).Count();

        var exportErrors = await _exportRepo.CountErrorsAsync(batchId);

        var customer = await _customerRepo.GetByIdAsync(batch.CustomerId);

        return new BatchDashboardItem
        {
            BatchId = batchId,
            SequenceNumber = batch.SequenceNumber,
            Customer = customer!,
            StartedAt = batch.StartedAt,
            Status = batch.Status,

            CurrentStep = currentStep?.Step ?? "",
            StepStatus = currentStep?.Status ?? StepStatus.Pending,

            HeronImport = new StepMetrics
            {
                Total = rawTotal,
                Success = rawTotal
            },

            Farmadati = new StepMetrics
            {
                Total = rawTotal,
                Success = enrichedTotal
            },

            Suppliers = new StepMetrics
            {
                Total = rawTotal,
                Success = resolvedTotal
            },

            Magento = new StepMetricsMagento
            {
                Total = exportTotal,
                Success = exportSuccess,
                Errors = exportErrors,
                InsertImages = exportInsertImage,
                Insert = exportInsert,
                UpdatePrice = exportUpdatePrice,
                Pending = exportPending
            }
        };
    }
}
