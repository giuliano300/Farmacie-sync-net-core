using MongoDB.Bson;
using MongoDB.Driver;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Enums;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class ExportRepository : IExportRepository
{
    private readonly MongoContext _context;

    public ExportRepository(MongoContext context)
    {
        _context = context;
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
            x => x.Id == ObjectId.Parse(id) &&
            x.Aic == aic,
            update);
    }

    public async Task SetErrorAsync(string id, string aic, string error)
    {
        var update = Builders<ExportExecution>.Update
            .Set(x => x.Status, ExportStatus.Error)
            .Set(x => x.LastAttemptAt, DateTime.UtcNow)
            .Set(x => x.ErrorMessage, error)
            .Inc(x => x.AttemptCount, 1);

        await _context.ExportExecutions.UpdateOneAsync(
            x => x.Id == ObjectId.Parse(id) &&
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
}
