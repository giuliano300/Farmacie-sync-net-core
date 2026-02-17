using HeronIntegration.Shared.Entities;

public interface ISupplierStockRepository
{
    Task InsertManyAsync(IEnumerable<SupplierStock> items);
    Task ReplaceSupplierAsync(string supplierCode, IEnumerable<SupplierStock> items);

    Task<List<SupplierStock>> GetByAicAsync(string aic);
    Task<List<SupplierStock>> GetByAicsAsync(List<string> aics);
}
