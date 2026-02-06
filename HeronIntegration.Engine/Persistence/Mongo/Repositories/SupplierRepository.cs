using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

public class SupplierRepository : ISupplierRepository
{
    private readonly MongoContext _context;

    public SupplierRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task InsertAsync(Supplier supplier)
    {
        await _context.Suppliers.InsertOneAsync(supplier);
    }

    public async Task<List<Supplier>> GetActiveAsync()
    {
        return await _context.Suppliers
            .Find(x => x.Active)
            .ToListAsync();
    }

    public async Task<List<Supplier>> GetAllAsync()
        => await _context.Suppliers.Find(_ => true).ToListAsync();

    public async Task<Supplier?> GetByIdAsync(string id)
        => await _context.Suppliers.Find(x => x.Id == ObjectId.Parse(id)).FirstOrDefaultAsync();

    public async Task UpdateAsync(Supplier supplier)
        => await _context.Suppliers.ReplaceOneAsync(x => x.Id == supplier.Id, supplier);

    public async Task DeleteAsync(string id)
        => await _context.Suppliers.DeleteOneAsync(x => x.Id == ObjectId.Parse(id));

}
