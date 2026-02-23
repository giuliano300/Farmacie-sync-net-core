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
            // =============================
            // 1️⃣ CARICAMENTO METADATI
            // =============================
            var manufacturersTask = _exporter.GetAttributeOptionsAsync("manufacturer");
            var suppliersTask = _exporter.GetAttributeOptionsAsync("supplier");
            var categoriesTask = _exporter.GetCategoryMapAsync();

            await Task.WhenAll(manufacturersTask, suppliersTask, categoriesTask);

            var manufacturersRaw = manufacturersTask.Result;
            var suppliersRaw = suppliersTask.Result;
            var categoriesRaw = categoriesTask.Result;

            var manufacturers = manufacturersRaw
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key.Trim(),
                    g => g.First().Value,
                    StringComparer.OrdinalIgnoreCase
                );

            var suppliers = suppliersRaw
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key.Trim(),
                    g => g.First().Value,
                    StringComparer.OrdinalIgnoreCase
                );

            var categories = categoriesRaw
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key.Trim(),
                    g => g.First().Value,
                    StringComparer.OrdinalIgnoreCase
                );
            
            // =============================
            // 2️⃣ CARICAMENTO PRODOTTI RISOLTI
            // =============================
            var resolvedList = await _resolvedRepo.GetByBatchAsync(batchId);

            var mappedList = resolvedList
                .Select(p =>
                {
                    var mapped = new ResolvedProduct
                    {
                        Aic = p.Aic,
                        Name = p.Name,
                        Price = p.Price,
                        Availability = p.Availability,
                        LongDescription = p.LongDescription,
                        ShortDescription = p.ShortDescription,

                        // 🔹 Supplier
                        SupplierCode = (!string.IsNullOrWhiteSpace(p.SupplierCode) &&
                                        suppliers.TryGetValue(p.SupplierCode, out var supplierId))
                            ? supplierId.ToString()
                            : "0",

                        // 🔹 Manufacturer
                        Producer = (!string.IsNullOrWhiteSpace(p.Producer) &&
                                    manufacturers.TryGetValue(p.Producer, out var manufacturerId))
                            ? manufacturerId.ToString()
                            : "0",

                        // 🔹 Categoria
                        SubCategory = (!string.IsNullOrWhiteSpace(p.SubCategory) &&
                                       ResolveCategoryId(categories, p.SubCategory) is int categoryId)
                            ? categoryId.ToString()
                            : null,

                        Images = p.Images
                    };

                    return mapped;
                })
                .ToList();


            // =============================
            // 3️⃣ UPSERT PRODOTTI (PARALLELO)
            // =============================
            await _exporter.ImportProductsAsync(mappedList);

            // =============================
            // 4️⃣ UPLOAD IMMAGINI (PARALLELO LIMITATO)
            // =============================
            var semaphore = new SemaphoreSlim(4);

            var imageTasks = resolvedList.Select(async p =>
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

            // =============================
            // 5️⃣ ESECUZIONE CRON MAGENTO
            // =============================
            await _exporter.RunMagentoCronAsync();

            // =============================
            // 5️⃣ FINALIZZAZIONE
            // =============================
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
