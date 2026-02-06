using HeronIntegration.Shared.Entities;

public interface IResolvedProductRepository
{
    Task InsertManyAsync(IEnumerable<ResolvedProduct> items);

    Task<List<ResolvedProduct>> GetByBatchAsync(string batchId);
}
