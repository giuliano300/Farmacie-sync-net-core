using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;

namespace HeronIntegration.Engine.StepProcessors;

public class SupplierResolutionStepProcessor : IStepProcessor
{
    // Must match the supplier resolution step generated for each batch.
    public string Step => "Suppliers";

    private readonly IEnrichedProductRepository _enrichedRepo;
    private readonly ISupplierStockRepository _supplierRepo;
    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly IStepRepository _stepRepo;
    private readonly ICleanupService _cleanupService;


    public SupplierResolutionStepProcessor(
        IEnrichedProductRepository enrichedRepo,
        ISupplierStockRepository supplierRepo,
        IResolvedProductRepository resolvedRepo,
        IStepRepository stepRepo,
        ICleanupService cleanupService)
    {
        _enrichedRepo = enrichedRepo;
        _supplierRepo = supplierRepo;
        _resolvedRepo = resolvedRepo;
        _stepRepo = stepRepo;
        _cleanupService = cleanupService;
    }

    /// <summary>
    /// Chooses the best stock source for each enriched product and creates resolved products.
    /// </summary>
    public async Task<StepExecutionResult> ExecuteAsync(string batchId, CancellationToken token, TypeRun? type = null)
    {
        var result = new StepExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Clear stale export status before rebuilding resolved products.
            await _cleanupService.updateExportExecution(batchId);

            var step = await _stepRepo.GetStepAsync(batchId, "Suppliers");
            if (step == null)
            {
                result.ErrorMessage = "Nessun step trovato";
                return result;
            }

            await _stepRepo.SetRunningAsync(step.Id.ToString());

            var raws = await _enrichedRepo.GetByBatchAsync(batchId);

            if (raws == null || raws.Count == 0)
                return result;

            var batchObjectId = ObjectId.Parse(batchId);

            // Collect all needed AICs so supplier stock can be loaded with one query.
            var aics = raws.Select(x => x.Aic).Distinct().ToList();

            // One bulk query avoids an N+1 lookup per product.
            var supplierStocks = await _supplierRepo.GetByAicsAsync(aics);

            // Supplier data is used only for availability: prefer the supplier
            // with the highest stock and never use its price for Magento.
            var bestSupplierByAic = supplierStocks
                .Where(a=>a.Availability > 0)
                .GroupBy(x => x.Aic)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(x => x.Availability)
                        .ThenBy(x => x.SupplierCode)
                        .First()
                );

            var resolvedList = new List<ResolvedProduct>(raws.Count);

            foreach (var raw in raws)
            {
                // Heron stock is always the default candidate.
                var chosen = new SupplierStock
                {
                    SupplierCode = "HERON",
                    Aic = raw.Aic,
                    Price = raw.HeronPrice,
                    Availability = raw.HeronStock
                };

                // Alternative suppliers are used only when Heron has no availability.
                // Keep the Heron price: suppliers contribute stock only.
                if (bestSupplierByAic.TryGetValue(raw.Aic, out var best) && chosen.Availability == 0)
                {
                    chosen = new SupplierStock
                    {
                        SupplierCode = best.SupplierCode,
                        Aic = raw.Aic,
                        Price = raw.HeronPrice,
                        Availability = best.Availability
                    };
                }

                resolvedList.Add(ResolvedProduct.MapToResolved(raw, chosen, batchObjectId));
            }

            if (resolvedList.Count > 0)
                await _resolvedRepo.InsertManyAsync(resolvedList);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        result.FinishedAt = DateTime.UtcNow;

        return result;

    }
}
