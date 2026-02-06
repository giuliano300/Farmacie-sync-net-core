using HeronIntegration.Shared.Entities;

public interface ISupplierStockRepository
{
    Task InsertManyAsync(IEnumerable<SupplierStock> items);
    Task ReplaceSupplierDatasetAsync(string supplierCode, IEnumerable<SupplierStock> items);

    Task<List<SupplierStock>> GetByAicAsync(string aic);
}
