using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Shared.Entities;
using MongoDB.Driver;

public class SupplierStockRepository : ISupplierStockRepository
{
    private readonly MongoContext _context;

    public SupplierStockRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task InsertManyAsync(IEnumerable<SupplierStock> items)
    {
        await _context.SupplierStocks.InsertManyAsync(items);
    }

    public async Task<List<SupplierStock>> GetByAicAsync(string aic)
    {
        return await _context.SupplierStocks
            .Find(x => x.Aic == aic)
            .ToListAsync();
    }

    public async Task ReplaceSupplierDatasetAsync(
    string supplierCode,
    IEnumerable<SupplierStock> items)
    {
        await _context.SupplierStocks.DeleteManyAsync(x => x.SupplierCode == supplierCode);

        await _context.SupplierStocks.InsertManyAsync(items);
    }

}
