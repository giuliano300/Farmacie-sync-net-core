using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using System.Collections.Concurrent;

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
            var manufacturersTask = _exporter.GetAttributeOptionsAsync("manufacturer");
            var suppliersTask = _exporter.GetAttributeOptionsAsync("supplier");
            var categoriesTask = _exporter.GetCategoryMapAsync();

            await Task.WhenAll(manufacturersTask, suppliersTask, categoriesTask);

            var manufacturers = manufacturersTask.Result
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Value,
                    StringComparer.OrdinalIgnoreCase
                );

            var suppliers = suppliersTask.Result
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Value,
                    StringComparer.OrdinalIgnoreCase
                );

            var categories = new Dictionary<string, int>(
                categoriesTask.Result,
                StringComparer.OrdinalIgnoreCase
            );

            var resolvedList = await _resolvedRepo.GetByBatchAsync(batchId);

            // ===== STEP 1 BUILD PRODUCT BATCH =====
            foreach (var p in resolvedList)
            {
                if (!string.IsNullOrWhiteSpace(p.SupplierCode) &&
                    suppliers.TryGetValue(p.SupplierCode.ToLowerInvariant(), out var supplierId))
                    p.SupplierCode = supplierId.ToString();
                else
                    p.SupplierCode = "0";

                if (!string.IsNullOrWhiteSpace(p.Producer) &&
                    manufacturers.TryGetValue(p.Producer.ToLowerInvariant(), out var manufacturerId))
                    p.Producer = manufacturerId.ToString();
                else
                    p.Producer = "0";

                var categoryId = ResolveCategoryId(categories, p.SubCategory!.ToLowerInvariant());
                if (categoryId != null)
                    p.SubCategory = categoryId.ToString();
            }

            var batchFile = await _exporter.BuildProductBatchAsync(resolvedList);

            _exporter.UploadBatchToMagento(batchFile);

            // ===== STEP 2 TRIGGER MAGENTO IMPORT =====
            await _exporter.TriggerBatchImportAsync(batchFile);

            // ===== STEP 3 BULK INVENTORY =====
            await _exporter.BulkInventoryAsync(
                resolvedList.Select(x => new InventoryItem
                {
                    Sku = x.Aic,
                    Qty = x.Availability
                })
            );

            // ===== STEP 4 IMAGES =====
            var semaphore = new SemaphoreSlim(4);

            var tasks = resolvedList.Select(async p =>
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

            await Task.WhenAll(tasks);

            // ===== FINALIZZAZIONE BATCH =====
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
            return null;

        return match.Value;
    }

}
