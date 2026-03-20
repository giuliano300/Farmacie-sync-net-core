using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;

namespace HeronIntegration.Engine.StepProcessors;

public class SupplierResolutionStepProcessor : IStepProcessor
{
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

    public async Task<StepExecutionResult> ExecuteAsync(string batchId, CancellationToken token, TypeRun? type = null)
    {
        var result = new StepExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {

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

            // 1. Recupero tutti gli AIC necessari
            var aics = raws.Select(x => x.Aic).Distinct().ToList();

            // 2. Carico tutti gli stock supplier in UNA sola query
            var supplierStocks = await _supplierRepo.GetByAicsAsync(aics);

            // 3. Miglior supplier (prezzo minimo) per AIC
            var bestSupplierByAic = supplierStocks
                .Where(a=>a.Availability > 0)
                .GroupBy(x => x.Aic)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.Price).First()
                );

            var resolvedList = new List<ResolvedProduct>(raws.Count);

            foreach (var raw in raws)
            {
                // HERON sempre candidato
                var chosen = new SupplierStock
                {
                    SupplierCode = "HERON",
                    Aic = raw.Aic,
                    Price = raw.HeronPrice,
                    Availability = raw.HeronStock
                };

                // supplier alternativi
                if (bestSupplierByAic.TryGetValue(raw.Aic, out var best) && chosen.Availability == 0)
                       chosen = best;

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
