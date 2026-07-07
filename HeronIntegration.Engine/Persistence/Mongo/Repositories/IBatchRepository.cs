using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Models;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IBatchRepository
{

    Task<List<BatchExecution>> GetLastAsync(int limit);
    Task<BatchExecution?> GetRunningBatchAsync(string customerId);
    Task<BatchExecution?> GetByTriggerReasonAsync(string customerId, string triggerReason);

    Task<List<BatchExecution>> GetRunningAsync();

    Task<int> GetNextSequenceAsync(string customerId);

    Task<string> CreateAsync(BatchExecution batch);

    Task CloseAsync(string batchId);

    Task<BatchExecution?> GetByIdAsync(string batchId);

    Task SetRunningAsync(string batchId);

    Task<bool> CanStartNextStepAsync(string batchId);
    Task UpdateDownloadProducts(string batchId, int totalMagentoProducts, int totalDownloadMagentoProducts);
    Task UpdatProcessId(string batchId, int processId);

    Task<(BatchExecution? batch, StepExecution? step)> GetRunningBatchWithStepAsync();

    Task<StepExecution?> GetCurrentStepAsync(string batchId);

    Task<List<BatchExecution>> GetTodayAsync(string? customerId);
    Task<List<BatchExecution>> GetTodayForCustomerAsync(string customerId);
    Task<List<BatchExecution>> GetAllPastBatchByCustomerId(string customerId);
    Task<(List<BatchExecution> Items, long TotalCount)> GetPastBatchPageByCustomerId(string customerId, int pageIndex, int pageSize);

    Task<List<BatchExecution>> GetAllTodayClosed();

    Task<BatchDashboardItem> BuildBatchDashboard(BatchExecution batch);
    Task<List<BatchDashboardItem>> BuildBatchDashboards(List<BatchExecution> batches);

    Task DeleteAsync(string id);

    Task<List<BatchExecution>> GetOpenBatchesAsync(DateTime? yesterday = null);
    Task<List<BatchExecution>> GetOpenStartedBeforeAsync(DateTime startedBeforeUtc);

}
