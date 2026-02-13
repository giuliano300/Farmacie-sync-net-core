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

    public FarmadatiEnrichmentStepProcessor(
        IRawProductRepository rawRepo,
        IEnrichedProductRepository enrichedRepo,
        IProductEnrichmentService enrichmentService,
        IFarmadatiCacheRepository farmadatiCacheRepo)
    {
        _rawRepo = rawRepo;
        _enrichedRepo = enrichedRepo;
        _enrichmentService = enrichmentService;
        _farmadatiCacheRepo = farmadatiCacheRepo;
    }

    public async Task<StepExecutionResult?> ExecuteAsync(string batchId)
    {
        var result = new StepExecutionResult();
        result.StartedAt = DateTime.Now;
        try
        {
            var raws = await _rawRepo.GetByBatchAsync(batchId);

            var existing = await _enrichedRepo.GetAicsByBatchAsync(batchId);
            var existingSet = existing.ToHashSet();

            var cacheList = await _farmadatiCacheRepo.GetByAicsAsync(raws.Select(x => x.Aic));
            var cacheDict = cacheList.ToDictionary(x => x.Aic, x => x);

            var enrichedList = new List<EnrichedProduct>();
            var newCacheList = new List<FarmadatiCache>();

            foreach (var raw in raws)
            {
                if (existingSet.Contains(raw.Aic))
                    continue;

                EnrichedProduct enriched;

                if (cacheDict.TryGetValue(raw.Aic, out var cache))
                {
                    enriched = EnrichedProduct.FromCache(raw, cache, batchId);
                }
                else
                {
                    EnrichedProduct? enrichment = null;

                    try
                    {
                        enrichment = await _enrichmentService.EnrichAsync(
                            raw.Aic,
                            raw.CustomerId,
                            batchId);
                    }
                    catch { }

                    enriched = enrichment ?? EnrichedProduct.CreateMinimal(raw, batchId);

                    newCacheList.Add(new FarmadatiCache
                    {
                        Aic = enriched.Aic,
                        Name = enriched.Name,
                        ShortDescription = enriched.ShortDescription!,
                        LongDescription = enriched.LongDescription!,
                        Images = enriched.Images,
                        CachedAt = DateTime.UtcNow
                    });
                }

                enrichedList.Add(enriched);
            }

            if (enrichedList.Count > 0)
                await _enrichedRepo.InsertManyAsync(enrichedList);

            if (newCacheList.Count > 0)
                await _farmadatiCacheRepo.InsertManyAsync(newCacheList);
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