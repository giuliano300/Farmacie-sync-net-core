using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Concurrent;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class BatchRepository : IBatchRepository
{
    private readonly MongoContext _context;
    private readonly IStepRepository _stepRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IRawProductRepository _rawRepo;
    private readonly IHostEnvironment _env;
    private readonly IEnrichedProductRepository _enrichedRepo;
    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly IExportRepository _exportRepo;
    private readonly BatchProcessManager _processManager;

    public BatchRepository(
        MongoContext context,
        IStepRepository stepRepo,
        IHostEnvironment env,
        ICustomerRepository customerRepo,
        IRawProductRepository rawRepo,
        IEnrichedProductRepository enrichedRepo,
        IResolvedProductRepository resolvedRepo,
        IExportRepository exportRepo,
        BatchProcessManager processManager
        )
    {
        _context = context;
        _stepRepo = stepRepo;
        _env = env;
        _customerRepo = customerRepo;
        _rawRepo = rawRepo;
        _enrichedRepo = enrichedRepo;
        _resolvedRepo = resolvedRepo;
        _exportRepo = exportRepo;
        _processManager = processManager;
    }

    public async Task<List<BatchExecution>> GetLastAsync(int limit)
    {
        return await _context.BatchExecutions
            .Find(_ => true)
            .SortByDescending(x => x.StartedAt)
            .Limit(limit)
            .ToListAsync();
    }
    public async Task<List<BatchExecution>> GetAllPastBatchByCustomerId(string customerId)
    {
        var todayStart = DateTime.UtcNow.Date;

        return await _context.BatchExecutions
            .Find(x => x.CustomerId == customerId && (x.StartedAt < todayStart || x.Status == BatchStatus.Closed))
            .SortByDescending(x => x.StartedAt)
            .ToListAsync();
    }
    public async Task<BatchExecution?> GetByIdAsync(string batchId)
    {
        return await _context.BatchExecutions
            .Find(x => x.Id == ObjectId.Parse(batchId))
            .FirstOrDefaultAsync();
    }

    public async Task<BatchExecution?> GetRunningBatchAsync(string customerId)
    {
        return await _context.BatchExecutions
            .Find(x => x.CustomerId == customerId && x.Status == BatchStatus.Running)
            .FirstOrDefaultAsync();
    }


    public async Task<List<BatchExecution>> GetRunningAsync()
    {
        return await _context.BatchExecutions
            .Find(x => x.Status == BatchStatus.Running)
            .ToListAsync();
    }

    public async Task<int> GetNextSequenceAsync(string customerId)
    {
        var last = await _context.BatchExecutions
            .Find(x => x.CustomerId == customerId)
            .SortByDescending(x => x.SequenceNumber)
            .FirstOrDefaultAsync();

        return (last?.SequenceNumber ?? 0) + 1;
    }

    public async Task<string> CreateAsync(BatchExecution batch)
    {
        await _context.BatchExecutions.InsertOneAsync(batch);
        return batch.Id.ToString();
    }

    public async Task CloseAsync(string batchId)
    {
        var filter = Builders<BatchExecution>.Filter.Eq(x => x.Id, ObjectId.Parse(batchId));

        var update = Builders<BatchExecution>.Update
            .Set(x => x.Status, BatchStatus.Closed)
            .Set(x => x.ClosedAt, DateTime.UtcNow);

        await _context.BatchExecutions.UpdateOneAsync(filter, update);
    }

    public async Task UpdateDownloadProducts(string batchId, int totalMagentoProducts, int totalDownloadMagentoProducts)
    {
        var filter = Builders<BatchExecution>.Filter.Eq(x => x.Id, ObjectId.Parse(batchId));

        var update = Builders<BatchExecution>.Update
            .Set(x => x.totalMagentoProducts, totalMagentoProducts)
            .Set(x => x.totalDownloadMagentoProducts, totalDownloadMagentoProducts);

        await _context.BatchExecutions.UpdateOneAsync(filter, update);
    }

    public async Task<List<StepExecution>> GetByBatchAsync(string batchId)
    {
        return await _context.StepExecutions
            .Find(x => x.BatchId == ObjectId.Parse(batchId))
            .ToListAsync();
    }

    public async Task SetRunningAsync(string batchId)
    {
        await _context.BatchExecutions.UpdateOneAsync(
            x => x.Id == ObjectId.Parse(batchId),
            Builders<BatchExecution>.Update
                .Set(x => x.Status, BatchStatus.Running)
                .Set(x => x.StartedAt, DateTime.UtcNow)
        );
    }

    public async Task<StepExecution?> GetCurrentStepAsync(string batchId)
    {
        return await _context.StepExecutions
            .Find(x => x.BatchId == ObjectId.Parse(batchId))
            .SortByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CanStartNextStepAsync(string batchId)
    {
        var lastStep = await _context.StepExecutions
            .Find(x => x.BatchId == ObjectId.Parse(batchId))
            .SortByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync();

        if (lastStep == null)
            return true;

        return lastStep.Status == StepStatus.Success;
    }

    public async Task<(BatchExecution? batch, StepExecution? step)> GetRunningBatchWithStepAsync()
    {
        var batch = await _context.BatchExecutions
            .Find(x => x.Status == BatchStatus.Running)
            .SortByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync();

        if (batch == null)
            return (null, null);

        var step = await _context.StepExecutions
            .Find(x => x.BatchId == batch.Id)
            .SortByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync();

        return (batch, step);
    }


    public async Task<List<BatchExecution>> GetTodayAsync()
    {
        var todayStart = DateTime.UtcNow.Date;
        var tomorrow = todayStart.AddDays(1);

        return await _context.BatchExecutions
            .Find(x => x.StartedAt >= todayStart && x.StartedAt < tomorrow && x.Status == BatchStatus.Running)
            .SortByDescending(x => x.StartedAt)
            .ToListAsync();
    }

    public async Task<List<BatchExecution>> GetAllTodayClosed()
    {
        var todayStart = DateTime.UtcNow.Date;
        var tomorrow = todayStart.AddDays(1);

        return await _context.BatchExecutions
            .Find(x => x.StartedAt >= todayStart && x.StartedAt < tomorrow && x.Status == BatchStatus.Closed)
            .SortByDescending(x => x.StartedAt)
            .ToListAsync();
    }

    public async Task<List<BatchExecution>> GetTodayForCustomerAsync(string customerId)
    {
        var todayStart = DateTime.UtcNow.Date;
        var tomorrow = todayStart.AddDays(1);

        return await _context.BatchExecutions
            .Find(x =>
                x.CustomerId == customerId &&
                x.StartedAt >= todayStart &&
                x.StartedAt < tomorrow)
            .SortByDescending(x => x.StartedAt)
            .ToListAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var objectId = ObjectId.Parse(id);

        _processManager.Stop(id);

        // cancella i prodotti raw collegati al batch
        await _context.RawProducts.DeleteManyAsync(x => x.BatchId == objectId);

        // cancella i prodotti enriched collegati al batch
        await _context.EnrichedProducts.DeleteManyAsync(x => x.BatchId == objectId);

        // cancella i prodotti resolved collegati al batch
        await _context.ResolvedProducts.DeleteManyAsync(x => x.BatchId == objectId);

        // cancella i prodotti export collegati al batch
        await _context.ExportExecutions.DeleteManyAsync(x => x.BatchId == objectId);

        // cancella gli step
        await _context.StepExecutions.DeleteManyAsync(x => x.BatchId == objectId);

        // cancella il report
        await _context.BatchExecutions.DeleteOneAsync(x => x.Id == objectId);
    }
    public async Task<BatchDashboardItem> BuildBatchDashboard(BatchExecution batch)
    {
        var batchId = batch.Id.ToString();

        // Query parallele
        var stepsTask = _stepRepo.GetByBatchAsync(batchId);
        var rawTask = _rawRepo.CountByBatchAsync(batchId);
        var enrichedTask = _enrichedRepo.CountByBatchAsync(batchId);
        var resolvedTask = _resolvedRepo.CountByBatchAsync(batchId);
        var exportTask = _exportRepo.GetByBatchAsync(batchId);
        var exportErrorsTask = _exportRepo.CountErrorsAsync(batchId);
        var customerTask = _customerRepo.GetByIdAsync(batch.CustomerId);

        await Task.WhenAll(
            stepsTask,
            rawTask,
            enrichedTask,
            resolvedTask,
            exportTask,
            exportErrorsTask,
            customerTask
        );

        var steps = stepsTask.Result;
        var rawTotal = rawTask.Result;
        var enrichedTotal = enrichedTask.Result;
        var resolvedTotal = resolvedTask.Result;
        var export = exportTask.Result;
        var exportErrors = exportErrorsTask.Result;
        var customer = customerTask.Result;

        var currentStep = steps
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();

        var exportTotal = export.Count();

        // calcolo metriche export in un solo passaggio
        int exportPending = 0;
        int exportSuccess = 0;
        int exportInsert = 0;
        int exportUpdatePrice = 0;
        int exportError = 0;

        foreach (var e in export)
        {
            switch (e.Status)
            {
                case ExportStatus.Pending:
                    exportPending++;
                    break;

                case ExportStatus.Success:
                    exportSuccess++;
                    break;

                case ExportStatus.Insert:
                    exportInsert++;
                    break;

                case ExportStatus.UpdatePrice:
                    exportUpdatePrice++;
                    break;

                //case ExportStatus.InsertImages:
                //    exportInsertImage++;
                //    break;

                case ExportStatus.Error:
                    exportError++;
                    break;
            }
        }

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
                totalDownloadMagentoProducts = batch.totalDownloadMagentoProducts,
                totalMagentoProducts = batch.totalMagentoProducts,
                Total = exportTotal,
                Success = exportSuccess,
                Errors = exportErrors,
                Insert = exportInsert,
                UpdatePrice = exportUpdatePrice,
                Pending = exportPending
            }
        };
    }
}
