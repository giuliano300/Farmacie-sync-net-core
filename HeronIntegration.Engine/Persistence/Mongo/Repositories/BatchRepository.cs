using MongoDB.Bson;
using MongoDB.Driver;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Enums;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class BatchRepository : IBatchRepository
{
    private readonly MongoContext _context;

    public BatchRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<List<BatchExecution>> GetLastAsync(int limit)
    {
        return await _context.BatchExecutions
            .Find(_ => true)
            .SortByDescending(x => x.StartedAt)
            .Limit(limit)
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
}
