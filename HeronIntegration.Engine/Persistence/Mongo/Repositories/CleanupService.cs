using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class CleanupService : ICleanupService
{
    private readonly IRawProductRepository _rawRepo;
    private readonly IEnrichedProductRepository _enrichedRepo;
    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly MongoContext _context;

    public CleanupService(
    IRawProductRepository rawRepo,
    IEnrichedProductRepository enrichedRepo,
    IResolvedProductRepository resolvedRepo,
    MongoContext context)
    {
        _rawRepo = rawRepo;
        _enrichedRepo = enrichedRepo;
        _resolvedRepo = resolvedRepo;
        _context = context;
    }

    public async Task CleanupBatchAsync(string batchId)
    {
        await _rawRepo.DeleteByBatchAsync(batchId);
        await _enrichedRepo.DeleteByBatchAsync(batchId);
        await _resolvedRepo.DeleteByBatchAsync(batchId);
    }
    public async Task CleanupPipeLineAsync(string step, string batchId)
    {
        if (step == "HeronImport")
        {
            await _rawRepo.DeleteByBatchAsync(batchId);
            await _enrichedRepo.DeleteByBatchAsync(batchId);
            await _resolvedRepo.DeleteByBatchAsync(batchId);
            await updateExportExecution(batchId);
        }

        if (step == "Farmadati")
        {
            await _enrichedRepo.DeleteByBatchAsync(batchId);
            await _resolvedRepo.DeleteByBatchAsync(batchId);
            await updateExportExecution(batchId);
        }

        if (step == "Suppliers")
        {
            await _resolvedRepo.DeleteByBatchAsync(batchId);
            await updateExportExecution(batchId);

        }

        if (step == "Magento")
            await updateExportExecution(batchId);
    }

    private async Task updateExportExecution(string batchId)
    {
        await _context.ExportExecutions.UpdateManyAsync(
            x => x.BatchId == ObjectId.Parse(batchId),
            Builders<ExportExecution>.Update
                .Set(x => x.Status, ExportStatus.Pending)
                .Set(x => x.LastAttemptAt, null)
        );
    }

}
