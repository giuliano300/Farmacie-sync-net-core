using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Suppliers
{
    public interface ISupplierStockRepository
    {
        Task ReplaceSupplierAsync(string supplierCode,
                                  IEnumerable<SupplierStock> rows);
    }
}
