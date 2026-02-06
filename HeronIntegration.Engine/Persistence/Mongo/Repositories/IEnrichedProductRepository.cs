using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IEnrichedProductRepository
{
    Task<EnrichedProduct?> GetByAicAsync(string aic);

    Task InsertAsync(EnrichedProduct product);

    Task InsertManyAsync(IEnumerable<EnrichedProduct> products);

    Task<List<EnrichedProduct>> GetByBatchAsync(string batchId);
}
