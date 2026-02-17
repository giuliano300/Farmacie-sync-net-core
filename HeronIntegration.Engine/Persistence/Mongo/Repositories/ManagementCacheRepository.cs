using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using MongoDB.Driver;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class ManagementCacheRepository : IManagementCacheRepository
{
    private readonly MongoContext _context;

    public ManagementCacheRepository(MongoContext context)
    {
        _context = context;
    }
    public async Task<ManagementCache?> GetAsync(string aic)
    {
        return await _context.ManagementCaches
            .Find(x => x.Aic == aic)
            .FirstOrDefaultAsync();
    }

    public async Task InsertAsync(ManagementCache cache)
    {
        await _context.ManagementCaches.InsertOneAsync(cache);
    }

    public async Task InsertManyAsync(IEnumerable<ManagementCache> cache)
    {
        await _context.ManagementCaches.InsertManyAsync(cache);
    }

    public async Task<List<ManagementCache>> GetByAicsAsync(IEnumerable<string> aics)
    {
        var filter = Builders<ManagementCache>.Filter
            .In(x => x.Aic, aics);

        return await _context.ManagementCaches
            .Find(filter)
            .ToListAsync();
    }
}
