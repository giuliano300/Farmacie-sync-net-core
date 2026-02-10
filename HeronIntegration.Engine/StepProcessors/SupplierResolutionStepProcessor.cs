using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;

namespace HeronIntegration.Engine.StepProcessors;

public class SupplierResolutionStepProcessor : IStepProcessor
{
    public string Step => "Suppliers";

    private readonly IRawProductRepository _rawRepo;
    private readonly ISupplierStockRepository _supplierRepo;
    private readonly IResolvedProductRepository _resolvedRepo;

    public SupplierResolutionStepProcessor(
        IRawProductRepository rawRepo,
        ISupplierStockRepository supplierRepo,
        IResolvedProductRepository resolvedRepo)
    {
        _rawRepo = rawRepo;
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
            var raws = await _rawRepo.GetByBatchAsync(batchId);

            var resolvedList = new List<ResolvedProduct>();

            foreach (var raw in raws)
            {
                SupplierStock? chosen = null;

                // 1️⃣ se Heron disponibile → usa Heron
                if (raw.Stock > 0)
                {
                    chosen = new SupplierStock
                    {
                        SupplierCode = "HERON",
                        Aic = raw.Aic,
                        Price = raw.Price,
                        Availability = raw.Stock,
                        Priority = int.MaxValue
                    };
                }
                else
                {
                    // 5 cerca fornitori alternativi
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
                    ResolvedAt = DateTime.UtcNow
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
