using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using Renci.SshNet;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

public class MagentoExportStepProcessor : IStepProcessor
{
    public string Step => "Magento";

    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly IExportRepository _exportRepo;
    private readonly IMagentoExporter _exporter;
    private readonly IBatchFinalizerService _batchFinalizer;

    public MagentoExportStepProcessor(
        IResolvedProductRepository resolvedRepo,
        IExportRepository exportRepo,
        IMagentoExporter exporter,
        IBatchFinalizerService batchFinalizer)
    {
        _resolvedRepo = resolvedRepo;
        _exportRepo = exportRepo;
        _exporter = exporter;
        _batchFinalizer = batchFinalizer;
    }

    public async Task<StepExecutionResult> ExecuteAsync(string batchId)
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
            var manufacturersTask = _exporter.GetAttributeOptionsAsync("manufacturer");
            var suppliersTask = _exporter.GetAttributeOptionsAsync("supplier");
            var categoriesTask = _exporter.GetCategoryMapAsync();
            var magentoProductsTask = _exporter.GetMagentoProductsSlimAsync();

            await Task.WhenAll(
                manufacturersTask,
                suppliersTask,
                categoriesTask,
                magentoProductsTask
            );

            var manufacturers = manufacturersTask.Result
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Value,
                    StringComparer.OrdinalIgnoreCase
                );

            var suppliers = suppliersTask.Result
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Value,
                    StringComparer.OrdinalIgnoreCase
                );

            var categories = categoriesTask.Result
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Value,
                    StringComparer.OrdinalIgnoreCase
                );

            var magentoProducts = magentoProductsTask.Result;
            var magentoDict = magentoProducts
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
                                    suppliers.TryGetValue(p.SupplierCode, out var supplierId))
                                        ? supplierId.ToString()
                                        : "0",
                    Producer = (!string.IsNullOrWhiteSpace(p.Producer) &&
                                manufacturers.TryGetValue(p.Producer, out var manufacturerId))
                                        ? manufacturerId.ToString()
                                        : "0",
                    SubCategory = (!string.IsNullOrWhiteSpace(p.SubCategory) &&
                                   ResolveCategoryId(categories, p.SubCategory) is int categoryId)
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
            }

            // =====================================================
            // SKU DA DISABILITARE
            // =====================================================
            var toDisable = magentoProducts
                .Where(m => !mongoDict.ContainsKey(m.Sku))
                .Select(m => m.Sku)
                .ToList();


            // =====================================================
            //  UPSERT
            // =====================================================
            if (toUpsert.Any())
                await _exporter.ImportProductsAsync(toUpsert);


            // =====================================================
            //  UPDATE STOCK
            // =====================================================
            await _exporter.UpdateStockBulkAsync(
                mappedList.Select(p => new InventoryItem
                {
                    Sku = p.Aic,
                    Qty = p.Availability
                })
                .ToList());

            // =====================================================
            // 6 DISABILITAZIONE PRODOTTI MANCANTI
            // =====================================================
            if (toDisable.Any())
                await _exporter.DisableProductsAsync(toDisable);

            // =====================================================
            //  UPLOAD IMMAGINI SOLO PER UPSERT
            // =====================================================
            var semaphore = new SemaphoreSlim(4);

            var imageTasks = toUpsert.Select(async p =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await _exporter.UploadImagesAsync(p);
                    await _exportRepo.SetSuccessAsync(batchId, p.Aic);
                }
                catch (Exception ex)
                {
                    await _exportRepo.SetErrorAsync(batchId, p.Aic, ex.Message);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(imageTasks);

            // =====================================================
            // 8️⃣ CRON MAGENTO
            // =====================================================
            await _exporter.RunMagentoCronAsync();

            // =====================================================
            // 9️⃣ FINALIZZAZIONE
            // =====================================================
            await _batchFinalizer.FinalizeBatchAsync(batchId);

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
    private static int? ResolveCategoryId(
    Dictionary<string, int> categoryMap,
    string categoryName)
    {
        var match = categoryMap
            .FirstOrDefault(x =>
                x.Key.EndsWith("/" + categoryName, StringComparison.OrdinalIgnoreCase)
            );

        if (match.Equals(default(KeyValuePair<string, int>)))
        {
            var matchNoCat = categoryMap
                .FirstOrDefault(x =>
                    x.Key.EndsWith("/da smistare", StringComparison.OrdinalIgnoreCase)
                );
            if (matchNoCat.Equals(default(KeyValuePair<string, int>)))
                return null;
            return matchNoCat.Value;
        }
        return match.Value;
    }


}
