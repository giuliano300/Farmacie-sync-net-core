using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using MongoDB.Driver;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class FarmadatiCacheRepository : IFarmadatiCacheRepository
{
    private readonly MongoContext _context;

    public FarmadatiCacheRepository(MongoContext context)
    {
        _context = context;
    }
    public async Task<FarmadatiCache?> GetAsync(string aic)
    {
        return await _context.FarmadatiCaches
            .Find(x => x.Aic == aic)
            .FirstOrDefaultAsync();
    }

    public async Task InsertAsync(FarmadatiCache cache)
    {
        await _context.FarmadatiCaches.InsertOneAsync(cache);
    }

    public async Task InsertManyAsync(IEnumerable<FarmadatiCache> cache)
    {
        await _context.FarmadatiCaches.InsertManyAsync(cache);
    }

    public async Task<List<FarmadatiCache>> GetByAicsAsync(IEnumerable<string> aics)
    {
        var filter = Builders<FarmadatiCache>.Filter
            .In(x => x.Aic, aics);

        return await _context.FarmadatiCaches
            .Find(filter)
            .ToListAsync();
    }
}
