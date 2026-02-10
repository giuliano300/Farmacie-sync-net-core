using MongoDB.Bson;
using MongoDB.Driver;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Enums;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class StepRepository : IStepRepository
{
    private readonly MongoContext _context;

    public StepRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<List<StepExecution>> GetStepsAsync(string batchId)
    {
        return await _context.StepExecutions
            .Find(x => x.BatchId == ObjectId.Parse(batchId))
            .ToListAsync();
    }

    public async Task<StepExecution?> GetStepAsync(string batchId, string step)
    {
        return await _context.StepExecutions
            .Find(x => x.BatchId == ObjectId.Parse(batchId) && x.Step == step)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(StepExecution step)
    {
        await _context.StepExecutions.InsertOneAsync(step);
    }

    public async Task SetRunningAsync(string id)
    {
        var update = Builders<StepExecution>.Update
            .Set(x => x.Status, StepStatus.Running)
            .Set(x => x.StartedAt, DateTime.UtcNow)
            .Inc(x => x.AttemptCount, 1);

        await _context.StepExecutions.UpdateOneAsync(
            x => x.Id == ObjectId.Parse(id),
            update);
    }

    public async Task SetSuccessAsync(string id, DateTime? StartedAt, DateTime? EndedAt)
    {
        var update = Builders<StepExecution>.Update
            .Set(x => x.Status, StepStatus.Success)
            .Set(a => a.StartedAt, StartedAt)
            .Set(x => x.EndedAt, EndedAt);

        await _context.StepExecutions.UpdateOneAsync(
            x => x.Id == ObjectId.Parse(id),
            update);
    }

    public async Task SetErrorAsync(string id, string error)
    {
        var update = Builders<StepExecution>.Update
            .Set(x => x.Status, StepStatus.Error)
            .Set(x => x.EndedAt, DateTime.UtcNow)
            .Set(x => x.ErrorMessage, error);

        await _context.StepExecutions.UpdateOneAsync(
            x => x.Id == ObjectId.Parse(id),
            update);
    }

    public async Task<StepExecution?> GetNextPendingStepAsync(string batchId)
    {
        return await _context.StepExecutions
            .Find(x =>
                x.BatchId == ObjectId.Parse(batchId) &&
                x.Status == StepStatus.Pending)
            .SortBy(x => x.Step)
            .FirstOrDefaultAsync();
    }

    public async Task CreateDefaultStepsAsync(string batchId, string customerId)
    {
        var id = ObjectId.Parse(batchId);

        var steps = new List<StepExecution>
    {
        new StepExecution { Id = ObjectId.GenerateNewId(), BatchId = id, CustomerId = customerId, Step = "HeronImport", Status = StepStatus.Pending },
        new StepExecution { Id = ObjectId.GenerateNewId(), BatchId = id, CustomerId = customerId, Step = "Farmadati", Status = StepStatus.Pending },
        new StepExecution { Id = ObjectId.GenerateNewId(), BatchId = id, CustomerId = customerId, Step = "Suppliers", Status = StepStatus.Pending },
        new StepExecution { Id = ObjectId.GenerateNewId(), BatchId = id, CustomerId = customerId, Step = "Magento", Status = StepStatus.Pending }
    };

        await _context.StepExecutions.InsertManyAsync(steps);
    }

    public async Task ResetStepsAsync(string batchId)
    {
        await _context.StepExecutions.UpdateManyAsync(
            x => x.BatchId == ObjectId.Parse(batchId),
            Builders<StepExecution>.Update
                .Set(x => x.Status, StepStatus.Pending)
                .Set(x => x.StartedAt, null)
                .Set(x => x.EndedAt, null)
        );
    }

    public async Task<List<StepExecution>> GetByBatchAsync(string batchId)
    {
        return await _context.StepExecutions
            .Find(x => x.BatchId == ObjectId.Parse(batchId))
            .SortBy(x => x.Step)
            .ToListAsync();
    }
}
