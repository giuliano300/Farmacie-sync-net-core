using MongoDB.Bson;
using MongoDB.Driver;

namespace HeronIntegration.Engine.Persistence.Mongo;

/// <summary>
/// Implements compensating rollback for standalone MongoDB installations, where
/// multi-document transactions are unavailable. Backups are server-side collections
/// so large batch data is not materialized in application memory.
/// </summary>
public sealed class MongoCompensationService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoCompensationService> _logger;

    public MongoCompensationService(
        IMongoDatabase database,
        ILogger<MongoCompensationService> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<MongoBackup> CreateBackupAsync(
        string collectionName,
        BsonDocument filter,
        CancellationToken cancellationToken = default)
    {
        var backupName = $"_rollback_{collectionName}_{Guid.NewGuid():N}";
        var source = _database.GetCollection<BsonDocument>(collectionName);
        var pipeline = new EmptyPipelineDefinition<BsonDocument>()
            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(
                new BsonDocumentPipelineStageDefinition<BsonDocument, BsonDocument>(
                    new BsonDocument("$match", filter)))
            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(
                new BsonDocumentPipelineStageDefinition<BsonDocument, BsonDocument>(
                    new BsonDocument("$out", backupName)));

        await source.Aggregate(pipeline).ToListAsync(cancellationToken);
        _logger.LogInformation(
            "Creato backup rollback {BackupCollection} per {Collection}",
            backupName,
            collectionName);

        return new MongoBackup(collectionName, backupName, filter);
    }

    public async Task RestoreAsync(
        IEnumerable<MongoBackup> backups,
        CancellationToken cancellationToken = default)
    {
        foreach (var backup in backups.Reverse())
        {
            // Remove documents possibly written by the failed operation, then merge
            // the original snapshot back by _id without replacing unrelated records.
            var target = _database.GetCollection<BsonDocument>(backup.CollectionName);
            await target.DeleteManyAsync(backup.Filter, cancellationToken);

            var source = _database.GetCollection<BsonDocument>(backup.BackupCollectionName);
            var merge = new BsonDocument("$merge", new BsonDocument
            {
                { "into", backup.CollectionName },
                { "on", "_id" },
                { "whenMatched", "replace" },
                { "whenNotMatched", "insert" }
            });
            await source.Aggregate<BsonDocument>(
                new BsonDocument[] { merge }).ToListAsync(cancellationToken);

            _logger.LogWarning(
                "Rollback completato per {Collection} dal backup {BackupCollection}",
                backup.CollectionName,
                backup.BackupCollectionName);
        }
    }

    public async Task DropBackupsAsync(
        IEnumerable<MongoBackup> backups,
        CancellationToken cancellationToken = default)
    {
        foreach (var backup in backups)
        {
            try
            {
                await _database.DropCollectionAsync(backup.BackupCollectionName, cancellationToken);
            }
            catch (MongoCommandException ex) when (ex.CodeName == "NamespaceNotFound")
            {
                // The backup was already removed.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Impossibile eliminare la collection temporanea di rollback {BackupCollection}",
                    backup.BackupCollectionName);
            }
        }
    }
}

public sealed record MongoBackup(
    string CollectionName,
    string BackupCollectionName,
    BsonDocument Filter);
