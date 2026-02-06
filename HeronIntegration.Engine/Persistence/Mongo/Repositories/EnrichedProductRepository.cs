using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class EnrichedProductRepository : IEnrichedProductRepository
{
    private readonly MongoContext _context;

    public EnrichedProductRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<EnrichedProduct?> GetByAicAsync(string aic)
    {
        return await _context.EnrichedProducts
            .Find(x => x.Aic == aic)
            .FirstOrDefaultAsync();
    }

    public async Task InsertAsync(EnrichedProduct product)
    {
        await _context.EnrichedProducts.InsertOneAsync(product);
    }

    public async Task InsertManyAsync(IEnumerable<EnrichedProduct> products)
    {
        await _context.EnrichedProducts.InsertManyAsync(products);
    }

    public async Task<List<EnrichedProduct>> GetByBatchAsync(string batchId)
    {
        return await _context.EnrichedProducts
            .Find(x => x.BatchId == ObjectId.Parse(batchId))
            .ToListAsync();
    }
}
