using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class ProductToExcludeRepository : IProductToExcludeRepository
{
    private readonly MongoContext _context;

    public ProductToExcludeRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task InsertAsync(ProductToExclude product)
    {
        await _context.ProductToExclude.InsertOneAsync(product);
    }

    public async Task InsertManyAsync(IEnumerable<ProductToExclude> products)
    {
        await _context.ProductToExclude.InsertManyAsync(products);
    }

    public async Task<List<ProductToExclude>> GetByCustomerAsync(string CustomerId)
    {
        return await _context.ProductToExclude
            .Find(x => x.CustomerId == CustomerId)
            .ToListAsync();
    }

    public async Task<ProductToExclude?> GetByIdAsync(string Id)
    => await _context.ProductToExclude.Find(x => x.Id == Id).FirstOrDefaultAsync();

    public async Task DeleteByCustomerAsync(string CustomerId)
    {
        var filter = Builders<ProductToExclude>.Filter.Eq("CustomerId", CustomerId);
        await _context.ProductToExclude.DeleteManyAsync(filter);
    }

    public async Task UpdateAsync(ProductToExclude p)
    => await _context.ProductToExclude.ReplaceOneAsync(x => x.Id == p.Id, p);

    public async Task DeleteAsync(string Id)
        => await _context.ProductToExclude.DeleteOneAsync(x => x.Id == Id);


}
