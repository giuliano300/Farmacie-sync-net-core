using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using System.Linq.Expressions;

namespace HeronIntegration.Engine.StepProcessors;

public class FarmadatiEnrichmentStepProcessor : IStepProcessor
{
    public string Step => "Farmadati";

    private readonly IRawProductRepository _rawRepo;
    private readonly IEnrichedProductRepository _enrichedRepo;
    private readonly IProductEnrichmentService _enrichmentService;
    private readonly IFarmadatiCacheRepository _farmadatiCacheRepo;
    private readonly IManagementCacheRepository _managementCacheRepo;
    private readonly IStepRepository _stepRepo;

    public FarmadatiEnrichmentStepProcessor(
        IRawProductRepository rawRepo,
        IEnrichedProductRepository enrichedRepo,
        IProductEnrichmentService enrichmentService,
        IFarmadatiCacheRepository farmadatiCacheRepo,
        IManagementCacheRepository managementCacheRepo,
        IStepRepository stepRepo)
    {
        _rawRepo = rawRepo;
        _enrichedRepo = enrichedRepo;
        _enrichmentService = enrichmentService;
        _farmadatiCacheRepo = farmadatiCacheRepo;
        _managementCacheRepo = managementCacheRepo;
        _stepRepo = stepRepo;
    }

    public async Task<StepExecutionResult?> ExecuteAsync(string batchId)
    {
        var result = new StepExecutionResult();
        result.StartedAt = DateTime.Now;
        try
        {
            var step = await _stepRepo.GetStepAsync(batchId, "Farmadati");
            if (step == null)
            {
                result.ErrorMessage = "Nessun step trovato";
                return result;
            }

            await _stepRepo.SetRunningAsync(step.Id.ToString());

            var raws = await _rawRepo.GetByBatchAsync(batchId);

            var existing = await _enrichedRepo.GetAicsByBatchAsync(batchId);
            var existingSet = existing.ToHashSet();

            var cacheList = await _farmadatiCacheRepo.GetByAicsAsync(raws.Select(x => x.Aic));
            var cacheDict = cacheList.ToDictionary(x => x.Aic, x => x);

            var cacheManagementList = await _managementCacheRepo.GetByAicsAsync(raws.Select(x => x.Aic));
            var cacheManagementDict = cacheManagementList.ToDictionary(x => x.Aic, x => x);

            var enrichedList = new List<EnrichedProduct>();

            foreach (var raw in raws)
            {
                if (existingSet.Contains(raw.Aic))
                    continue;

                EnrichedProduct enriched;
                EnrichedProduct? enrichment = null;

                if (cacheDict.TryGetValue(raw.Aic, out var cache))
                {
                    enriched = EnrichedProduct.FromCache(raw, cache, batchId);
                }
                else if(cacheManagementDict.TryGetValue(raw.Aic, out var cacheManagement))
                {
                    enriched = EnrichedProduct.CreateMinimal(raw, batchId);
                }
                else
                {

                    try
                    {
                        enrichment = await _enrichmentService.EnrichAsync(
                            raw.Aic,
                            raw.CustomerId,
                            batchId);
                    }
                    catch 
                    {
                        
                    }

                    if(enrichment != null)
                    {
                        var cached = new FarmadatiCache
                        {
                            Aic = enrichment.Aic,
                            Name = enrichment.Name,
                            ShortDescription = enrichment.ShortDescription!,
                            LongDescription = enrichment.LongDescription!,
                            Images = enrichment.Images,
                            CachedAt = DateTime.UtcNow
                        };
                        await _farmadatiCacheRepo.InsertAsync(cached);
                    }
                    else
                    {
                        var managementCache = new ManagementCache
                        {
                            Aic = raw.Aic,
                            CachedAt = DateTime.UtcNow
                        };
                        await _managementCacheRepo.InsertAsync(managementCache);

                    }

                    enriched = enrichment ?? EnrichedProduct.CreateMinimal(raw, batchId);

                }

                enrichedList.Add(enriched);
            }

            if (enrichedList.Count > 0)
                await _enrichedRepo.InsertManyAsync(enrichedList);

            result.Success = true;
        }
        catch (Exception ex) 
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        result.FinishedAt = DateTime.Now;
        return result;
    }
}