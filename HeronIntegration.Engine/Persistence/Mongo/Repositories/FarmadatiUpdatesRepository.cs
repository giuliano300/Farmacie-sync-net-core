using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.External.Farmadati.Generated;
using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Concurrent;

public class FarmadatiUpdatesRepository : IFarmadatiUpdatesRepository
{
    private readonly MongoContext _context;
    private readonly IProductEnrichmentService _productService;
    private readonly IFarmadatiCacheRepository _farmadatiCacheRepo;
    private readonly BatchProcessManager _processManager;

    public FarmadatiUpdatesRepository(MongoContext context, IProductEnrichmentService productService, IFarmadatiCacheRepository farmadatiCacheRepo, BatchProcessManager processManager)
    {
        _context = context;
        _productService = productService;
        _farmadatiCacheRepo = farmadatiCacheRepo;
        _processManager = processManager;
    }

    public async Task<List<FarmadatiUpdates>?> FindAsync()
    {
        return await _context.FarmadatiUpdates
           .Find(_ => true)
           .ToListAsync();
    }

    public async Task<FarmadatiUpdates?> GetByIdAsync(string id)
    {
        return await _context.FarmadatiUpdates
            .Find(x => x.Id== id)
            .FirstOrDefaultAsync();
    }
    public async Task CreateAsync(FarmadatiUpdates updates, CancellationToken token)
    {
        await _context.FarmadatiUpdates.InsertOneAsync(updates);

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessFarmadatiCache(updates, token);
            }
            catch (OperationCanceledException)
            {
                // STOP voluto → non è errore
                updates.EndedAt = DateTime.UtcNow;
                await UpdateAsync(updates.Id!, updates);
            }
            catch (Exception ex)
            {
                // errore reale → qui logghi
                updates.EndedAt = DateTime.UtcNow;
                await UpdateAsync(updates.Id!, updates);

                // TODO: log4net
                Console.WriteLine(ex);
            }
        });
    }
    private async Task ProcessFarmadatiCache(FarmadatiUpdates updates, CancellationToken token)
    {
        var farmadatiCache = await _farmadatiCacheRepo.GetAll();

        var chunks = farmadatiCache.Chunk(200).ToList();

        int worked = 0;

        updates.productNumber = farmadatiCache.Count;
        await UpdateAsync(updates.Id!, updates);

        foreach (var chunk in chunks)
        {
            token.ThrowIfCancellationRequested();

            var updatedCaches = new ConcurrentBag<FarmadatiCache>();

            await Parallel.ForEachAsync(chunk, new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = 10
            },
            async (f, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                var enriched = await _productService.EnrichAsync(
                    f.Aic,
                    ObjectId.GenerateNewId().ToString(),
                    ObjectId.GenerateNewId().ToString());

                if (enriched == null) return;

                f.Name = enriched.Name;
                f.ShortDescription = enriched.ShortDescription;
                f.LongDescription = enriched.LongDescription;
                f.Images = enriched.Images;
                f.CachedAt = DateTime.UtcNow;

                updatedCaches.Add(f);
            });

            await _farmadatiCacheRepo.UpdateManyAsync(updatedCaches);

            worked += updatedCaches.Count;

            updates.productWorked = worked;
            await UpdateAsync(updates.Id!, updates);
        }

        updates.EndedAt = DateTime.UtcNow;
        await UpdateAsync(updates.Id!, updates);
    }

    public async Task UpdateAsync(string id, FarmadatiUpdates updates)
    {
        await _context.FarmadatiUpdates.ReplaceOneAsync(
            x => x.Id == id,
            updates);
    }

    public async Task DeleteAsync(string id)
    {
        _processManager.Stop(ProcessType.Farmadati, id);

        await _context.FarmadatiUpdates.DeleteOneAsync(
            x => x.Id == id);
    }
}
