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
    private readonly MongoCompensationService _compensation;
    private readonly ILogger<CleanupService> _logger;

    public CleanupService(
    IRawProductRepository rawRepo,
    IEnrichedProductRepository enrichedRepo,
    IResolvedProductRepository resolvedRepo,
    MongoContext context,
    MongoCompensationService compensation,
    ILogger<CleanupService> logger)
    {
        _rawRepo = rawRepo;
        _enrichedRepo = enrichedRepo;
        _resolvedRepo = resolvedRepo;
        _context = context;
        _compensation = compensation;
        _logger = logger;
    }

    public async Task CleanupBatchAsync(string batchId)
    {
        await _rawRepo.DeleteByBatchAsync(batchId);
        await _enrichedRepo.DeleteByBatchAsync(batchId);
        await _resolvedRepo.DeleteByBatchAsync(batchId);
    }

    public async Task<int> CleanupExpiredBatchesAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        // batch_report is the retention authority: only completed batches having a
        // report older than the cutoff are eligible for cascading deletion.
        var batchIds = await _context.BatchReports
            .Find(x => x.FinishedAt < cutoffUtc)
            .Project(x => x.BatchId)
            .ToListAsync(cancellationToken);

        batchIds = batchIds
            .Where(batchId => ObjectId.TryParse(batchId, out _))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (batchIds.Count == 0)
            return 0;

        var objectIds = batchIds.Select(ObjectId.Parse).ToList();
        var objectIdFilter = new BsonDocument(
            "BatchId",
            new BsonDocument("$in", new BsonArray(objectIds)));
        var stringIdFilter = new BsonDocument(
            "BatchId",
            new BsonDocument("$in", new BsonArray(batchIds)));
        var executionFilter = new BsonDocument(
            "_id",
            new BsonDocument("$in", new BsonArray(objectIds)));
        var backups = new List<MongoBackup>();

        try
        {
            // Create every snapshot before the first delete. If snapshot creation
            // fails, no production data has been modified yet.
            foreach (var (collection, filter) in new[]
            {
                ("step_execution", objectIdFilter),
                ("export_execution", objectIdFilter),
                ("raw_product", objectIdFilter),
                ("enriched_product", objectIdFilter),
                ("resolved_product", objectIdFilter),
                ("import_to_magento_status", stringIdFilter),
                ("batch_report", stringIdFilter),
                ("batch_execution", executionFilter)
            })
            {
                backups.Add(await _compensation.CreateBackupAsync(
                    collection,
                    filter,
                    cancellationToken));
            }

            // Delete dependent documents first, then their reports and batch executions.
            await _context.StepExecutions.DeleteManyAsync(
                Builders<StepExecution>.Filter.In(x => x.BatchId, objectIds), cancellationToken);
            await _context.ExportExecutions.DeleteManyAsync(
                Builders<ExportExecution>.Filter.In(x => x.BatchId, objectIds), cancellationToken);
            await _context.RawProducts.DeleteManyAsync(
                Builders<RawProduct>.Filter.In(x => x.BatchId, objectIds), cancellationToken);
            await _context.EnrichedProducts.DeleteManyAsync(
                Builders<EnrichedProduct>.Filter.In(x => x.BatchId, objectIds), cancellationToken);
            await _context.ResolvedProducts.DeleteManyAsync(
                Builders<ResolvedProduct>.Filter.In(x => x.BatchId, objectIds), cancellationToken);
            await _context.ImportToMagentoStatus.DeleteManyAsync(
                Builders<ImportToMagentoStatus>.Filter.In(x => x.BatchId, batchIds), cancellationToken);
            await _context.BatchReports.DeleteManyAsync(
                Builders<BatchReport>.Filter.In(x => x.BatchId, batchIds), cancellationToken);
            await _context.BatchExecutions.DeleteManyAsync(
                Builders<BatchExecution>.Filter.In(x => x.Id, objectIds), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pulizia batch scaduti fallita; avvio rollback");
            try
            {
                await _compensation.RestoreAsync(backups, CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                _logger.LogCritical(rollbackException, "Rollback pulizia batch scaduti fallito");
                throw new AggregateException(ex, rollbackException);
            }

            throw;
        }
        finally
        {
            await _compensation.DropBackupsAsync(backups, CancellationToken.None);
        }

        return batchIds.Count;
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

    public async Task updateExportExecution(string batchId, ExportStatus status = ExportStatus.Pending)
    {
        await _context.ExportExecutions.UpdateManyAsync(
            x => x.BatchId == ObjectId.Parse(batchId),
            Builders<ExportExecution>.Update
                .Set(x => x.Status, status)
                .Set(x => x.LastAttemptAt, null)
        );

        await _context.BatchExecutions.UpdateManyAsync(
            x => x.Id == ObjectId.Parse(batchId),
            Builders<BatchExecution>.Update
                .Set(x => x.totalMagentoProducts, null)
                .Set(x => x.totalDownloadMagentoProducts, null));
    }

}
