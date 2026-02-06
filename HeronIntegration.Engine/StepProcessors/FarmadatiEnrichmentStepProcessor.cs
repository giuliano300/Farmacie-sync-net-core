using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.StepProcessors;

public class FarmadatiEnrichmentStepProcessor : IStepProcessor
{
    public string StepName => "Farmadati";

    private readonly IRawProductRepository _rawRepo;
    private readonly IEnrichedProductRepository _enrichedRepo;
    private readonly IProductEnrichmentService _enrichmentService;

    public FarmadatiEnrichmentStepProcessor(
        IRawProductRepository rawRepo,
        IEnrichedProductRepository enrichedRepo,
        IProductEnrichmentService enrichmentService)
    {
        _rawRepo = rawRepo;
        _enrichedRepo = enrichedRepo;
        _enrichmentService = enrichmentService;
    }

    public async Task ExecuteAsync(string batchId)
    {
        var raws = await _rawRepo.GetByBatchAsync(batchId);

        foreach (var raw in raws)
        {
            // cache check
            var cached = await _enrichedRepo.GetByAicAsync(raw.Aic);
            if (cached != null)
                continue;

            var enriched = await _enrichmentService.EnrichAsync(
            raw.Aic,
            raw.CustomerId,
            batchId);

            if (enriched != null)
                await _enrichedRepo.InsertAsync(enriched);
        }
    }
}
