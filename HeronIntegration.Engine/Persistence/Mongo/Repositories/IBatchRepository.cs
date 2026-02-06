using HeronIntegration.Engine.Persistence.Mongo.Documents;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IBatchRepository
{

    Task<List<BatchExecution>> GetLastAsync(int limit);
    Task<BatchExecution?> GetRunningBatchAsync(string customerId);

    Task<List<BatchExecution>> GetRunningAsync();

    Task<int> GetNextSequenceAsync(string customerId);

    Task<string> CreateAsync(BatchExecution batch);

    Task CloseAsync(string batchId);

    Task<BatchExecution?> GetByIdAsync(string batchId);

    Task SetRunningAsync(string batchId);

}
