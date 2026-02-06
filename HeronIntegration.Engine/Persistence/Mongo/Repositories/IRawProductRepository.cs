using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IRawProductRepository
{
    Task InsertAsync(RawProduct product);

    Task InsertManyAsync(IEnumerable<RawProduct> products);

    Task<List<RawProduct>> GetByBatchAsync(string batchId);

    Task<List<RawProduct>> GetPendingForResolutionAsync(string batchId);
}
