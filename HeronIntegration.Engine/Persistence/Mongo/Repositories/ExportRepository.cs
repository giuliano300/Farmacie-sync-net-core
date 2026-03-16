using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class ExportRepository : IExportRepository
{
    private readonly MongoContext _context;
    private readonly IBatchReportRepository _batchReport;

    public ExportRepository(MongoContext context, IBatchReportRepository batchReport)
    {
        _context = context;
        _batchReport = batchReport;
    }

    public async Task InsertManyAsync(IEnumerable<ExportExecution> exports)
    {
        await _context.ExportExecutions.InsertManyAsync(exports);
    }

    public async Task CreateAsync(ExportExecution export)
    {
        await _context.ExportExecutions.InsertOneAsync(export);
    }

    public async Task<List<ExportExecution>> GetPendingAsync(string batchId, int take)
    {
        return await _context.ExportExecutions
            .Find(x =>
                x.BatchId == ObjectId.Parse(batchId) &&
                x.Status != ExportStatus.Success)
            .Limit(take)
            .ToListAsync();
    }

    public async Task SetSuccessAsync(string id, string aic)
    {
        var update = Builders<ExportExecution>.Update
            .Set(x => x.Status, ExportStatus.Success)
            .Set(x => x.LastAttemptAt, DateTime.UtcNow)
            .Inc(x => x.AttemptCount, 1);

        await _context.ExportExecutions.UpdateOneAsync(
            x => x.BatchId == ObjectId.Parse(id) &&
            x.Aic == aic,
            update);
    }

    public async Task ChangeStatusAsync(
    string batchId,
    List<ResolvedProduct> products,
    ExportStatus status)
    {
        var aicList = products.Select(p => p.Aic).ToList();

        var filter = Builders<ExportExecution>.Filter.And(
            Builders<ExportExecution>.Filter.Eq(x => x.BatchId, ObjectId.Parse(batchId)),
            Builders<ExportExecution>.Filter.In(x => x.Aic, aicList)
        );

        var update = Builders<ExportExecution>.Update
            .Set(x => x.Status, status)
            .Set(x => x.LastAttemptAt, DateTime.UtcNow)
            .Inc(x => x.AttemptCount, 1);

        await _context.ExportExecutions.UpdateManyAsync(filter, update);
    }

    public async Task SetStatusAsync(string batchId, string aic, ExportStatus status)
    {
        var update = Builders<ExportExecution>.Update
            .Set(x => x.Status, status)
            .Set(x => x.LastAttemptAt, DateTime.UtcNow)
            .Inc(x => x.AttemptCount, 1);

        await _context.ExportExecutions.UpdateOneAsync(
            x => x.BatchId == ObjectId.Parse(batchId) && x.Aic == aic,
            update);
    }

    public async Task SetStatusBulkAsync(List<InventoryItem> items, ExportStatus status)
    {
        var skus = items.Select(x => x.Sku).ToList();
        var batchId = ObjectId.Parse(items[0].Id);

        var filter = Builders<ExportExecution>.Filter.And(
            Builders<ExportExecution>.Filter.Eq(x => x.BatchId, batchId),
            Builders<ExportExecution>.Filter.In(x => x.Aic, skus)
        );

        var update = Builders<ExportExecution>.Update
            .Set(x => x.Status, status)
            .Set(x => x.LastAttemptAt, DateTime.UtcNow)
            .Inc(x => x.AttemptCount, 1);

        await _context.ExportExecutions.UpdateManyAsync(filter, update);
    }

    public async Task SetErrorAsync(string id, string aic, string error)
    {
        var update = Builders<ExportExecution>.Update
            .Set(x => x.Status, ExportStatus.Error)
            .Set(x => x.LastAttemptAt, DateTime.UtcNow)
            .Set(x => x.ErrorMessage, error)
            .Inc(x => x.AttemptCount, 1);

        await _context.ExportExecutions.UpdateOneAsync(
            x => x.BatchId == ObjectId.Parse(id) &&
            x.Aic == aic,
            update);
    }

    public async Task<bool> ExistsAsync(string batchId, string aic)
    {
        return await _context.ExportExecutions
            .Find(x =>
                x.BatchId == ObjectId.Parse(batchId) &&
                x.Aic == aic)
            .AnyAsync();
    }

    public async Task ResetSingleAsync(string batchId, string aic)
    {
        await _context.ExportExecutions.UpdateOneAsync(
            x => x.BatchId == ObjectId.Parse(batchId) && x.Aic == aic,
            Builders<ExportExecution>.Update
                .Set(x => x.Status, ExportStatus.Pending)
                .Set(x => x.AttemptCount, 0)
                .Set(x => x.ErrorMessage, null)
        );
    }

    public async Task ResetBatchAsync(string batchId)
    {
        await _context.ExportExecutions.UpdateManyAsync(
            x => x.BatchId == ObjectId.Parse(batchId),
            Builders<ExportExecution>.Update
                .Set(x => x.Status, ExportStatus.Pending)
                .Set(x => x.AttemptCount, 0)
                .Set(x => x.ErrorMessage, null)
        );
    }

    public async Task SetStatusBatchAsync(string batchId, ExportStatus exportStatus = ExportStatus.Pending)
    {
        await _context.ExportExecutions.UpdateManyAsync(
            x => x.BatchId == ObjectId.Parse(batchId),
            Builders<ExportExecution>.Update
                .Set(x => x.Status, exportStatus)
        );
    }

    public async Task<BatchReport> BuildBatchReportAsync(string batchId)
    {
        var total = await _context.ExportExecutions.Find(
            x => x.BatchId == ObjectId.Parse(batchId)
        ).ToListAsync();

        var success = total.Count(a => a.Status == ExportStatus.Success);

        var import = total.Count(a => a.Status == ExportStatus.Insert || a.Status == ExportStatus.UpdatePrice);

        var prices = total.Count(a => a.Status == ExportStatus.UpdatePrice);

        var images = total.Count(a => a.Status == ExportStatus.InsertImages);

        var errors = total.Count(a => a.Status == ExportStatus.Error);

        var report = new BatchReport
        {
            BatchId = batchId,
            FinishedAt = DateTime.UtcNow,
            TotalProducts = (int)total.Count(),
            Insert = (int)import,
            UpdatePrice = (int)prices,
            InsertImages = (int)images,
            Complete = (int)success,
            Errors = (int)errors
        };

        await _batchReport.InsertOneAsync(report);

        return report;
    }

    public async Task<List<ExportExecution>> GetByBatchAsync(string batchId)
    {
        return await _context.ExportExecutions
            .Find(x => x.BatchId == ObjectId.Parse(batchId))
            .ToListAsync();
    }

    public async Task<int> CountByBatchAsync(string batchId)
    {
        return (int)await _context.ExportExecutions
            .CountDocumentsAsync(x => x.BatchId == ObjectId.Parse(batchId));
    }

    public async Task<int> CountSuccessAsync(string batchId)
    {
        return (int)await _context.ExportExecutions
            .CountDocumentsAsync(x =>
                x.BatchId == ObjectId.Parse(batchId) &&
                x.Status == ExportStatus.Success);
    }

    public async Task<int> CountErrorsAsync(string batchId)
    {
        return (int)await _context.ExportExecutions
            .CountDocumentsAsync(x =>
                x.BatchId == ObjectId.Parse(batchId) &&
                x.Status == ExportStatus.Error);
    }

    public async Task DeleteByBatchAsync(string batchId)
    {
        var filter = Builders<RawProduct>.Filter.Eq("BatchId", ObjectId.Parse(batchId));
        await _context.RawProducts.DeleteManyAsync(filter);
    }

}
