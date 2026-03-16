using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;

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

    Task ChangeStatusAsync(string batchId, List<ResolvedProduct> products, ExportStatus status);

    Task SetStatusAsync(string batchId, string aic, ExportStatus status);
    Task SetStatusBulkAsync(List<InventoryItem> items, ExportStatus status);

    Task<int> CountByBatchAsync(string batchId);

    Task<int> CountSuccessAsync(string batchId);

    Task<int> CountErrorsAsync(string batchId);

    Task<List<ExportExecution>> GetByBatchAsync(string batchId);

    Task SetStatusBatchAsync(string batchId, ExportStatus exportStatus = ExportStatus.Pending);

    Task DeleteByBatchAsync(string batchId);


}
