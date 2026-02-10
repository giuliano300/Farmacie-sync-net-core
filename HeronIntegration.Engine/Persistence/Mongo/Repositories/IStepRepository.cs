using HeronIntegration.Engine.Persistence.Mongo.Documents;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IStepRepository
{
    Task<List<StepExecution>> GetStepsAsync(string batchId);

    Task<StepExecution?> GetStepAsync(string batchId, string step);

    Task CreateAsync(StepExecution step);

    Task SetRunningAsync(string id);

    Task SetSuccessAsync(string id, DateTime? StartedAt, DateTime? EndedAt);

    Task SetErrorAsync(string id, string error);

    Task<StepExecution?> GetNextPendingStepAsync(string batchId);

    Task<List<StepExecution>> GetByBatchAsync(string batchId);
    Task CreateDefaultStepsAsync(string batchId, string customerId);
    Task ResetStepsAsync(string batchId);
}
