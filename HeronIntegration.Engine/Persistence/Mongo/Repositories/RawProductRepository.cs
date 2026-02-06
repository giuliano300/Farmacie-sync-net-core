using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class RawProductRepository : IRawProductRepository
{
    private readonly MongoContext _context;

    public RawProductRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task InsertAsync(RawProduct product)
    {
        await _context.RawProducts.InsertOneAsync(product);
    }

    public async Task InsertManyAsync(IEnumerable<RawProduct> products)
    {
        await _context.RawProducts.InsertManyAsync(products);
    }

    public async Task<List<RawProduct>> GetByBatchAsync(string batchId)
    {
        return await _context.RawProducts
            .Find(x => x.BatchId == ObjectId.Parse(batchId))
            .ToListAsync();
    }

    public async Task<List<RawProduct>> GetPendingForResolutionAsync(string batchId)
    {
        return await _context.RawProducts
            .Find(x => x.BatchId == ObjectId.Parse(batchId))
            .ToListAsync();
    }
}
