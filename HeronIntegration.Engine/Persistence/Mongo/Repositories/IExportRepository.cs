using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IExportRepository
{
    Task CreateAsync(ExportExecution export);

    Task InsertManyAsync(IEnumerable<ExportExecution> exports);

    Task<List<ExportExecution>> GetPendingAsync(string batchId, int take);

    Task SetSuccessAsync(string batchId, string aic);

    Task SetErrorAsync(string batchId, string aic, string error);

    Task<bool> ExistsAsync(string batchId, string aic);

    Task ResetSingleAsync(string batchId, string aic);
    Task ResetBatchAsync(string batchId);
    Task<BatchReport> BuildBatchReportAsync(string batchId);

}
