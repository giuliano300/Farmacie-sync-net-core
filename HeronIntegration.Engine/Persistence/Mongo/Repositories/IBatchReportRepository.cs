using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IBatchReportRepository
{

    Task InsertOneAsync(BatchReport report);

    Task<BatchReport> GetBatchesAsync(string batchId);


    Task<BatchReport?> GetByIdAsync(string id);
}
