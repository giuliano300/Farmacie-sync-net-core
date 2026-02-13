using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;

namespace HeronIntegration.Engine.StepProcessors;

public class SupplierResolutionStepProcessor : IStepProcessor
{
    public string Step => "Suppliers";

    private readonly IEnrichedProductRepository _enrichedRepo;
    private readonly ISupplierStockRepository _supplierRepo;
    private readonly IResolvedProductRepository _resolvedRepo;

    public SupplierResolutionStepProcessor(
        IEnrichedProductRepository enrichedRepo,
        ISupplierStockRepository supplierRepo,
        IResolvedProductRepository resolvedRepo)
    {
        _enrichedRepo = enrichedRepo;
        _supplierRepo = supplierRepo;
        _resolvedRepo = resolvedRepo;
    }

    public async Task<StepExecutionResult> ExecuteAsync(string batchId)
    {
        var result = new StepExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            var raws = await _enrichedRepo.GetByBatchAsync(batchId);

            var resolvedList = new List<ResolvedProduct>();

            foreach (var raw in raws)
            {
                SupplierStock? chosen = null;

                // Se Heron disponibile → usa Heron
                if (raw.HeronStock > 0)
                {
                    chosen = new SupplierStock
                    {
                        SupplierCode = "HERON",
                        Aic = raw.Aic,
                        Price = raw.HeronPrice,
                        Availability = raw.HeronStock,
                        Priority = int.MaxValue
                    };
                }
                else
                {
                    // Cerca fornitori alternativi
                    var alternatives = await _supplierRepo.GetByAicAsync(raw.Aic);

                    chosen = alternatives
                        .OrderByDescending(x => x.Priority)
                        .ThenBy(x => x.Price)
                        .FirstOrDefault();
                }

                if (chosen == null)
                    continue;

                resolvedList.Add(new ResolvedProduct
                {
                    Id = ObjectId.GenerateNewId(),
                    BatchId = ObjectId.Parse(batchId),
                    CustomerId = raw.CustomerId,
                    Aic = raw.Aic,
                    SupplierCode = chosen.SupplierCode,
                    Price = chosen.Price,
                    Availability = chosen.Availability,
                    Name = raw.Name,
                    ShortDescription = raw.ShortDescription,
                    LongDescription = raw.LongDescription,
                });
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
