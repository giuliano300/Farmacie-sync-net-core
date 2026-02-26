using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

[ApiController]
[Route("api/Magento")]
public class MagentoController : ControllerBase
{
    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly IExportRepository _exportRepo;
    private readonly IMagentoExporter _exporter;
    private readonly IBatchFinalizerService _batchFinalizer;

    public MagentoController(IResolvedProductRepository resolvedRepo,
        IExportRepository exportRepo,
        IMagentoExporter exporter,
        IBatchFinalizerService batchFinalizer)
    {
        _resolvedRepo = resolvedRepo;
        _exportRepo = exportRepo;
        _exporter = exporter;
        _batchFinalizer = batchFinalizer;
    }

    [HttpGet("")]
    public async Task<StepExecutionResult> MassiveImport(string batchId)
    {
        var result = new StepExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // =====================================================
            // CARICAMENTO METADATI MAGENTO
            // =====================================================
            var magentoMetatada = await _exporter.GetMagentoMetadataAsync();

            var magentoDict = magentoMetatada.magentoProducts!
                .ToDictionary(x => x.Sku, StringComparer.OrdinalIgnoreCase);

            // =====================================================
            //  CARICAMENTO PRODOTTI MONGO
            // =====================================================
            var resolvedList = await _resolvedRepo.GetByBatchAsync(batchId);

            var mappedList = resolvedList.Select(p =>
            {
                return new ResolvedProduct
                {
                    Aic = p.Aic,
                    Name = p.Name,
                    Price = p.Price,
                    Availability = p.Availability,
                    LongDescription = p.LongDescription,
                    ShortDescription = p.ShortDescription,
                    SupplierCode = (!string.IsNullOrWhiteSpace(p.SupplierCode) &&
                                    magentoMetatada.suppliers!.TryGetValue(p.SupplierCode, out var supplierId))
                                        ? supplierId.ToString()
                                        : "0",
                    Producer = (!string.IsNullOrWhiteSpace(p.Producer) &&
                                magentoMetatada.manufacturers!.TryGetValue(p.Producer, out var manufacturerId))
                                        ? manufacturerId.ToString()
                                        : "0",
                    SubCategory = (!string.IsNullOrWhiteSpace(p.SubCategory) &&
                                   _exporter.ResolveCategoryId(magentoMetatada.categories!, p.SubCategory) is int categoryId)
                                        ? categoryId.ToString()
                                        : null,
                    Images = p.Images
                };
            }).ToList();

            var mongoDict = mappedList
                .ToDictionary(x => x.Aic, StringComparer.OrdinalIgnoreCase);

            // =====================================================
            // CALCOLO UPSERT (insert + update)
            // =====================================================
            var toUpsert = new List<ResolvedProduct>();
            var toSkipUpsert = new List<ResolvedProduct>();

            foreach (var mongoProduct in mappedList)
            {
                if (!magentoDict.TryGetValue(mongoProduct.Aic, out var magentoProduct))
                {
                    toUpsert.Add(mongoProduct);
                    continue;
                }

                bool needsUpdate = false;

                if (magentoProduct.Price != mongoProduct.Price)
                    needsUpdate = true;

                if ((magentoProduct.Manufacturer ?? "") != (mongoProduct.Producer ?? ""))
                    needsUpdate = true;

                if ((magentoProduct.Supplier ?? "") != (mongoProduct.SupplierCode ?? ""))
                    needsUpdate = true;

                if ((magentoProduct.Description ?? "").Trim() !=
                    (mongoProduct.ShortDescription ?? "").Trim() && ((magentoProduct.Description ?? "").Trim() !=
                    (mongoProduct.LongDescription ?? "").Trim()))
                    needsUpdate = true;

                var mongoCats = new HashSet<string>(
                    new[] { mongoProduct.SubCategory! }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));

                var magentoCats = new HashSet<string>(
                    magentoProduct.Categories ?? new List<string>());

                if (!mongoCats.IsSubsetOf(magentoCats))
                    needsUpdate = true;

                if (needsUpdate)
                    toUpsert.Add(mongoProduct);
                else
                    toSkipUpsert.Add(mongoProduct);
            }

            // =====================================================
            // SKU DA DISABILITARE
            // =====================================================
            var toDisable = magentoMetatada.magentoProducts!
                .Where(m => !mongoDict.ContainsKey(m.Sku))
                .Select(m => m.Sku)
                .ToList();


            // =====================================================
            //  UPSERT
            // =====================================================
            if (toSkipUpsert.Any())
                await _exportRepo.ChangeStatusAsync(batchId, toSkipUpsert, ExportStatus.Insert);

            if (toUpsert.Any())
                await _exporter.ImportProductsAsync(toUpsert);


            // =====================================================
            //  UPDATE STOCK
            // =====================================================
            await UpdateStockBulkAsync(batchId);

            // =====================================================
            // DISABILITAZIONE PRODOTTI MANCANTI
            // =====================================================
            if (toDisable.Any())
                await _exporter.DisableProductsAsync(toDisable);

            // =====================================================
            //  UPLOAD IMMAGINI SOLO PER UPSERT
            // =====================================================
            var all = mappedList;
            await _exporter.UpdateImageBulkAsync(all);

            // =====================================================
            // CRON MAGENTO
            // =====================================================
            await _exporter.RunMagentoCronAsync();

            // =====================================================
            // FINALIZZAZIONE
            // =====================================================
            await _batchFinalizer.FinalizeBatchAsync(batchId);

            //await _exportRepo.ChangeStatusAsync(batchId, resolvedList, ExportStatus.Success);

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


    [HttpGet("updateStockBulk")]
    public async Task<StepExecutionResult> UpdateStockBulkAsync(string batchId)
    {
        var result = new StepExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            var inventory = await _resolvedRepo.GetByBatchAsync(batchId);
            var list = inventory.Select(p => new InventoryItem
            {
                Id = batchId,
                Sku = p.Aic,
                Qty = p.Availability
            }).ToList();

            await _exporter.UpdateStockBulkAsync(list);

        }
        catch (Exception e)
        {
            result.Success = false;
            result.ErrorMessage = e.Message;
        }

        result.FinishedAt = DateTime.UtcNow;

        return result;
    }

    [HttpGet("updateImageBulk")]
    public async Task<StepExecutionResult> UpdateImageBulkAsync(string batchId)
    {
        var result = new StepExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            var list = await _resolvedRepo.GetByBatchAsync(batchId);
            await _exporter.UpdateImageBulkAsync(list);

        }
        catch (Exception e)
        {
            result.Success = false;
            result.ErrorMessage = e.Message;
        }

        result.FinishedAt = DateTime.UtcNow;

        return result;
    }
}
