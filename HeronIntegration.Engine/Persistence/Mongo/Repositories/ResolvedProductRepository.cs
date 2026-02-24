using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class ResolvedProductRepository : IResolvedProductRepository
{
    private readonly MongoContext _context;

    public ResolvedProductRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task InsertManyAsync(IEnumerable<ResolvedProduct> items)
    {
        await _context.ResolvedProducts.InsertManyAsync(items);
    }

    public async Task<List<ResolvedProduct>> GetByBatchAsync(string batchId)
    {
        return await _context.ResolvedProducts
            .Find(x => x.BatchId == ObjectId.Parse(batchId))
            .ToListAsync();
    }

    public async Task<ResolvedProduct> GetById(string id)
    {
        return await _context.ResolvedProducts
            .Find(x => x.Id == ObjectId.Parse(id))
            .FirstOrDefaultAsync();
    }

    public async Task DeleteByBatchAsync(string batchId)
    {
        var filter = Builders<ResolvedProduct>.Filter.Eq("BatchId", batchId);
        await _context.ResolvedProducts.DeleteManyAsync(filter);
    }

}
