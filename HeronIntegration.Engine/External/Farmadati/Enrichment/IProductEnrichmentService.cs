using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.External.Farmadati.Enrichment;

public interface IProductEnrichmentService
{
    Task<EnrichedProduct?> EnrichAsync(string productCode, string customerId, string batchId);
}
