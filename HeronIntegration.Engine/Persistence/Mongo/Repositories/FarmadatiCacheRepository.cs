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
        try
        {
            await _context.FarmadatiCaches.InsertOneAsync(cache);
        }
        catch
        {

        }
    }

    public async Task InsertManyAsync(IEnumerable<FarmadatiCache> cache)
    {
        try
        {
            await _context.FarmadatiCaches.InsertManyAsync(cache);
        }
        catch
        {

        }
    }

    public async Task BulkWriteAsync(
    IEnumerable<WriteModel<FarmadatiCache>> writes)
    {
        await _context.FarmadatiCaches
            .BulkWriteAsync(writes);
    }

    public async Task UpdateManyAsync(IEnumerable<FarmadatiCache> updates)
    {
        var ids = updates.Select(x => x.Id).ToList();

        var filter = Builders<FarmadatiCache>.Filter.In(x => x.Id, ids);

        var update = Builders<FarmadatiCache>.Update
        .Set(x => x.CachedAt, DateTime.UtcNow);

        await _context.FarmadatiCaches.UpdateManyAsync(filter, update);
    }

    public async Task DeleteManyAsync(IEnumerable<FarmadatiCache> caches)
    {
        var ids = caches.Select(x => x.Id).ToList();

        var filter = Builders<FarmadatiCache>.Filter.In(x => x.Id, ids);

        await _context.FarmadatiCaches.DeleteManyAsync(filter);
    }

    public async Task BulkUpsertAsync(
    IEnumerable<FarmadatiCache> products)
    {
        var now = DateTime.UtcNow;

        var writes = new List<WriteModel<FarmadatiCache>>();

        foreach (var product in products)
        {
            var filter =
                Builders<FarmadatiCache>.Filter.Eq(
                    x => x.Aic,
                    product.Aic);

            var update =
                Builders<FarmadatiCache>.Update
                    .Set(x => x.Name, product.Name)
                    .Set(x => x.ShortDescription, product.ShortDescription)
                    .Set(x => x.LongDescription, product.LongDescription)
                    .Set(x => x.MacroGroup, product.MacroGroup)
                    .Set(x => x.MacroGroupCode, product.MacroGroupCode)
                    .Set(x => x.Images, product.Images)
                    .Set(x => x.UpdatedAt, now)
                    .Set(x => x.DatasetDate, product.DatasetDate)
                    .SetOnInsert(x => x.CachedAt, now);

            writes.Add(
                new UpdateOneModel<FarmadatiCache>(
                    filter,
                    update)
                {
                    IsUpsert = true
                });
        }

        if (writes.Count > 0)
        {
            await _context.FarmadatiCaches.BulkWriteAsync(
                writes,
                new BulkWriteOptions
                {
                    IsOrdered = false
                });
        }
    }

    public async Task<List<FarmadatiCache>> GetByAicsAsync(IEnumerable<string> aics)
    {
        var filter = Builders<FarmadatiCache>.Filter
            .In(x => x.Aic, aics);

        return await _context.FarmadatiCaches
            .Find(filter)
            .ToListAsync();
    }
    public async Task<List<FarmadatiCache>> GetAll()
    {
        return await _context.FarmadatiCaches
            .Find(_ => true)
            .ToListAsync();
    }
}
