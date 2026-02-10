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

    public FarmadatiEnrichmentStepProcessor(
        IRawProductRepository rawRepo,
        IEnrichedProductRepository enrichedRepo,
        IProductEnrichmentService enrichmentService)
    {
        _rawRepo = rawRepo;
        _enrichedRepo = enrichedRepo;
        _enrichmentService = enrichmentService;
    }

    public async Task<StepExecutionResult?> ExecuteAsync(string batchId)
    {
        var result = new StepExecutionResult();
        result.StartedAt = DateTime.Now;
        try
        {
            var raws = await _rawRepo.GetByBatchAsync(batchId);

            foreach (var raw in raws)
            {
                var exists = await _enrichedRepo.ExistsAsync(batchId, raw.Aic);
                if (exists)
                    continue;

                var enriched = await _enrichmentService.EnrichAsync(
                raw.Aic,
                raw.CustomerId,
                batchId);

                if (enriched == null)
                {
                    enriched = EnrichedProduct.CreateMinimal(raw, batchId);
                }
                
                await _enrichedRepo.InsertAsync(enriched);
            }

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