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

            var enrichedList = new List<EnrichedProduct>();

            foreach (var raw in raws)
            {
                var exists = await _enrichedRepo.ExistsAsync(batchId, raw.Aic);
                if (exists)
                    continue;

                var cache = await _farmadatiCacheRepo.GetAsync(raw.Aic);

                EnrichedProduct enriched;

                if (cache != null)
                {
                    // costruisci enriched dalla cache
                    enriched = EnrichedProduct.FromCache(raw, cache, batchId);
                }
                else
                {
                    var enrichment = new EnrichedProduct();
                    try
                    {
                        enrichment = await _enrichmentService.EnrichAsync(
                            raw.Aic,
                            raw.CustomerId,
                            batchId);
                    }
                    catch(Exception e)
                    {
                        enrichment = null;
                    };

                    if (enrichment == null)
                        enrichment = EnrichedProduct.CreateMinimal(raw, batchId);

                    enriched = enrichment;

                    // prepara cache (NON salvare json enriched)
                    var farmadatiCache = new FarmadatiCache
                    {
                        Aic = enriched.Aic,
                        Name = enriched.Name,
                        ShortDescription = enriched.ShortDescription!,
                        LongDescription = enriched.LongDescription!,
                        Images = enriched.Images,
                        CachedAt = DateTime.UtcNow
                    };

                   await _farmadatiCacheRepo.InsertAsync(farmadatiCache);
                }

                enrichedList.Add(enriched);
            }

            // bulk insert
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