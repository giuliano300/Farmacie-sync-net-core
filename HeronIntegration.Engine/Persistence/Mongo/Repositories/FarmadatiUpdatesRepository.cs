using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.External.Farmadati.Generated;
using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Concurrent;

public class FarmadatiUpdatesRepository : IFarmadatiUpdatesRepository
{
    private readonly MongoContext _context;
    private readonly IProductEnrichmentService _productService;
    private readonly IFarmadatiCacheRepository _farmadatiCacheRepo;
    private readonly BatchProcessManager _processManager;
    private readonly IHeronXmlParser _parser;
    private readonly ICustomerRepository _customerRepository;
    private readonly IHostEnvironment _env;
    private readonly IProductEnrichmentService _enrichmentService;
    private readonly IManagementCacheRepository _managementCacheRepo;


    public FarmadatiUpdatesRepository(
        MongoContext context, 
        IProductEnrichmentService productService, 
        IFarmadatiCacheRepository farmadatiCacheRepo, 
        BatchProcessManager processManager, 
        IHeronXmlParser parser, 
        ICustomerRepository customerRepository, 
        IHostEnvironment env, 
        IProductEnrichmentService enrichmentService,
        IManagementCacheRepository managementCacheRepo
        )
    {
        _context = context;
        _productService = productService;
        _farmadatiCacheRepo = farmadatiCacheRepo;
        _processManager = processManager;
        _parser = parser;
        _customerRepository = customerRepository;
        _env = env;
        _enrichmentService = enrichmentService;
        _managementCacheRepo = managementCacheRepo;
    }

    public async Task<List<FarmadatiUpdatesWithCustomer>> FindAsync()
    {
        var updates = await _context.FarmadatiUpdates
            .Find(_ => true)
            .ToListAsync();

        if (!updates.Any())
            return new List<FarmadatiUpdatesWithCustomer>();

        // 🔥 prendi tutti gli id cliente distinti
        var customerIds = updates
            .Select(x => x.CustomerId)
            .Distinct()
            .ToList();

        // 🔥 UNA SOLA query
        var customers = await _customerRepository.GetByIdsAsync(customerIds);

        // 🔥 dictionary per lookup O(1)
        var customerDict = customers.ToDictionary(x => x.Id, x => x);

        // 🔥 mapping finale
        var result = updates.Select(fc => new FarmadatiUpdatesWithCustomer
        {
            Customer = customerDict.GetValueOrDefault(fc.CustomerId)!,
            FarmadatiUpdate = fc
        }).ToList();

        return result;
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
        var customer = await _customerRepository.GetByIdAsync(updates.CustomerId);
        if (customer == null) return;

        var root = _env.ContentRootPath;
        var parent = Directory.GetParent(root)!.FullName;

        var folder = Path.Combine(parent, "HeronFolder", customer.HeronFolder);
        if (!Directory.Exists(folder)) return;

        var pathFile = Directory.GetFiles(folder).FirstOrDefault();
        if (pathFile == null) return;

        var parsed = _parser.Parse(pathFile, customer.Id).ToList();

        var cacheList = await _farmadatiCacheRepo.GetByAicsAsync(parsed.Select(x => x.Aic));
        var cacheAics = cacheList.Select(x => x.Aic).ToHashSet();

        var managementRepo = await _managementCacheRepo.GetByAicsAsync(parsed.Select(x => x.Aic));
        var managementRepoAics = managementRepo.Select(x => x.Aic).ToHashSet();

        var newItems = parsed
            .Where(x => !cacheAics.Contains(x.Aic))
            .ToList();

        newItems = newItems
            .Where(x=> !managementRepoAics.Contains(x.Aic))
            .ToList();

        int worked = 0;

        updates.productNumber = newItems.Count();
        if (newItems.Count() == 0)
        {
            updates.productWorked = 0;
            updates.EndedAt = DateTime.UtcNow;
            await UpdateAsync(updates.Id!, updates);

            return;
        }

        await UpdateAsync(updates.Id!, updates);

        var chunks = newItems.Chunk(50);

        foreach (var chunk in chunks)
        {
            token.ThrowIfCancellationRequested();

            var farmadatiList = new ConcurrentBag<FarmadatiCache>();
            var managementList = new ConcurrentBag<ManagementCache>();

            await Parallel.ForEachAsync(chunk, new ParallelOptions
            {
                MaxDegreeOfParallelism = 10,
                CancellationToken = token
            },
            async (raw, ct) =>
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var enrichment = await _enrichmentService.EnrichAsync(
                        raw.Aic,
                        raw.CustomerId,
                        ObjectId.GenerateNewId().ToString()
                    );

                    if (enrichment != null)
                    {
                        farmadatiList.Add(new FarmadatiCache
                        {
                            Aic = enrichment.Aic,
                            Name = enrichment.Name,
                            ShortDescription = enrichment.ShortDescription!,
                            LongDescription = enrichment.LongDescription!,
                            Images = enrichment.Images,
                            CachedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        managementList.Add(new ManagementCache
                        {
                            Aic = raw.Aic,
                            CachedAt = DateTime.UtcNow
                        });
                    }
                }
                catch
                {
                    // log se vuoi
                }
            });

            // 🚀 BULK INSERT
            if (farmadatiList.Any())
                await _farmadatiCacheRepo.InsertManyAsync(farmadatiList);

            if (managementList.Any())
                await _managementCacheRepo.InsertManyAsync(managementList);

            worked += farmadatiList.Count + managementList.Count;

            // 🔥 update ogni chunk, non ogni item
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
