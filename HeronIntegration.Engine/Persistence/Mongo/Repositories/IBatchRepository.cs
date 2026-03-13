using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Models;

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

    Task<bool> CanStartNextStepAsync(string batchId);
    Task UpdateDownloadProducts(string batchId, int totalMagentoProducts, int totalDownloadMagentoProducts);

    Task<(BatchExecution? batch, StepExecution? step)> GetRunningBatchWithStepAsync();

    Task<StepExecution?> GetCurrentStepAsync(string batchId);

    Task<List<BatchExecution>> GetTodayAsync();
    Task<List<BatchExecution>> GetTodayForCustomerAsync(string customerId);
    Task<List<BatchExecution>> GetAllPastBatchByCustomerId(string customerId);

    Task<BatchDashboardItem> BuildBatchDashboard(BatchExecution batch);

    Task DeleteAsync(string id);

}
