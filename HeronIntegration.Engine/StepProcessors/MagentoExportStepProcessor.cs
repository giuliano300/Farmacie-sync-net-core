using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using System.Collections.Concurrent;

public class MagentoExportStepProcessor : IStepProcessor
{
    // Must match the Magento step generated for each batch.
    public string Step => "Magento";

    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly IExportRepository _exportRepo;
    private readonly IBatchRepository _batchRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IBatchFinalizerService _batchFinalizer;
    private readonly IMagentoExporterFactory _magentoExporterFactory;
    private readonly IStepRepository _stepRepo;
    private readonly ICleanupService _cleanupService;
    private readonly IImportToMagentoStatusRepository _importToMagento;

    public MagentoExportStepProcessor(
        IResolvedProductRepository resolvedRepo,
        IExportRepository exportRepo,
        IBatchFinalizerService batchFinalizer,
        IBatchRepository batchRepo,
        ICustomerRepository customerRepo,
        IMagentoExporterFactory magentoExporterFactory,
        IStepRepository stepRepo,
        ICleanupService cleanupService,
        IImportToMagentoStatusRepository importToMagento
        )
    {
        _resolvedRepo = resolvedRepo;
        _exportRepo = exportRepo;
        _batchFinalizer = batchFinalizer;
        _batchRepo = batchRepo;
        _customerRepo = customerRepo;
        _magentoExporterFactory = magentoExporterFactory;
        _stepRepo = stepRepo;
        _cleanupService = cleanupService;
        _importToMagento = importToMagento;
    }

    /// <summary>
    /// Exports resolved products to Magento according to the requested run type.
    /// </summary>
    public async Task<StepExecutionResult> ExecuteAsync(string batchId, CancellationToken token, TypeRun? type = 0)
    {
        var result = new StepExecutionResult { StartedAt = DateTime.UtcNow };

        try
        {
            // Default manual/API execution runs the full Magento workflow.
            if (type == null)
                type = TypeRun.Completo;

            // Clear stale status before calculating what Magento must receive.
            await _cleanupService.updateExportExecution(batchId);

            var step = await _stepRepo.GetStepAsync(batchId, "Magento")
                ?? throw new Exception("Nessun step trovato");

            await _stepRepo.SetRunningAsync(step.Id.ToString());

            var batch = await _batchRepo.GetByIdAsync(batchId);
            var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId)
                ?? throw new Exception("Customer non trovato");

            if (customer.Magento == null)
                throw new Exception("Magento config mancante");

            var exporter = _magentoExporterFactory.Create(customer.Magento);
            var resolvedList = await _resolvedRepo.GetByBatchAsync(batchId);

            // Normalize products once before comparing them with Magento metadata.
            var mapped = MapProducts(resolvedList);

            var sku = mapped.Select(a => a.Aic).ToList();

            // Download Magento metadata once; the following phases reuse the same snapshot.
            var metadata = await exporter.GetMagentoMetadataAsync(batchId, token);
            var magentoSet = metadata.magentoProducts!
                .Select(x => x.Sku)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Create/update the progress document used by dashboard polling.
               await _importToMagento.Start(batchId, mapped.Count, type!);

            var skus = new List<string>();
            // Product import creates missing products and updates changed product data.
            if (type is TypeRun.Completo or TypeRun.ImportProdotti)
                skus = await HandleProductUpsert(metadata, mapped, exporter, batchId, token, type);

            // Price/quantity update can run independently from full product import.
            if (type is TypeRun.UpdatePrezzi)
                skus = (await HandleStockUpdate(metadata, mapped, exporter, batchId, token)!).Select(a => a.Sku).ToList();

            // Image import stages files on FTP and waits for the Magento custom API to finish.
            if (type is TypeRun.Completo or TypeRun.ImportImmagini)
            {
                var productWithImages = mapped.Where(a => a.Images.Count > 0).ToList();
                if(productWithImages.Count > 0)
                {
                    if (type is TypeRun.ImportImmagini)
                        skus = productWithImages.Select(a => a.Aic).ToList();
                    await exporter.ImportImagesToFtpBulkAsync(productWithImages!, customer, token);
                    await exporter.WaitPollingImagesAsync(batchId, token);
                }
            }

            if (skus.Count > 0)
            {
                // Reindex only SKUs touched by this run and then clean Magento caches.
                await _importToMagento.UpdateImportStatusAsync(batchId, 
                    insertProductsStatus: OperationsStatus.Ended,
                    updateProductsStatus: OperationsStatus.Ended,
                    insertImagesStatus: OperationsStatus.Ended,
                    reindexStatus: OperationsStatus.Running);

                await exporter.ReindexAsync(skus, batchId, token);
                await exporter.WaitReindexAsync(batchId, token);
                await exporter.CleanIndex(token);
                await exporter.CleanCache(token);
            }

            // Finalization writes the report and removes transient batch data.
            await _batchFinalizer.FinalizeBatchAsync(batchId);

            // Mark dashboard progress as fully completed after finalization.
            await _importToMagento.UpdateImportStatusAsync(batchId,
                    insertProductsStatus: OperationsStatus.Ended,
                    updateProductsStatus: OperationsStatus.Ended,
                    insertImagesStatus: OperationsStatus.Ended,
                    reindexStatus: OperationsStatus.Ended,
                    importStatus: OperationsStatus.Ended,
                    reindexPercent: 100);

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

    private List<ResolvedProduct> MapProducts(List<ResolvedProduct> source)
    {
        // Create detached product instances so downstream phases can safely normalize values.
        return source.Select(p => new ResolvedProduct
        {
            BatchId = p.BatchId,
            Aic = p.Aic,
            Name = p.Name,
            Price = p.Price,
            OriginalPrice = p.OriginalPrice,
            Availability = p.Availability,
            MagentoCategoryId = p.MagentoCategoryId,
            LongDescription = p.LongDescription?.Trim(),
            ShortDescription = p.ShortDescription?.Trim(),
            SupplierCode = p.SupplierCode,
            Producer = p.Producer,
            SubCategory = p.SubCategory,
            Weight = p.Weight,
            Images = p.Images,
            Vat = p.Vat,
            MacroGroup = p.MacroGroup
        }).ToList();
    }

    private async Task<List<string>> HandleProductUpsert(
        MagentoMetadata metadata,
        List<ResolvedProduct> mapped,
        IMagentoExporter exporter,
        string batchId,
        CancellationToken token,
        TypeRun? type)
    {
        // Magento lookups are dictionary-based because this method compares every resolved product.
        var magentoDict = metadata.magentoProducts!
            .ToDictionary(x => x.Sku, StringComparer.OrdinalIgnoreCase);

        var mappedList = mapped.Select(p =>
        {
            // Magento expects option ids for manufacturer/supplier attributes, not display names.
            return new ResolvedProduct
            {
                BatchId = p.BatchId,
                Aic = p.Aic,
                Name = p.Name,
                Price = p.Price,
                OriginalPrice = p.OriginalPrice,
                Vat = p.Vat,
                Availability = p.Availability,
                LongDescription = p.LongDescription,
                ShortDescription = p.ShortDescription,
                SupplierCode = (!string.IsNullOrWhiteSpace(p.SupplierCode) &&
                                metadata.suppliers!.TryGetValue(p.SupplierCode, out var supplierId))
                                    ? supplierId.ToString()
                                    : "0",
                Producer = (!string.IsNullOrWhiteSpace(p.Producer) &&
                            metadata.manufacturers!.TryGetValue(p.Producer, out var manufacturerId))
                                    ? manufacturerId.ToString()
                                    : "0",
                SubCategory = p.SubCategory,
                MagentoCategoryId = p.MagentoCategoryId,
                Weight = p.Weight,
                Images = p.Images,
                MacroGroup = p.MacroGroup
            };
        }).ToList();

        var toUpsert = new List<ResolvedProduct>();
        var toSkip = new List<ResolvedProduct>();

        foreach (var p in mappedList)
        {
            // Missing SKU is a straight insert.
            if (!magentoDict.TryGetValue(p.Aic, out var m))
            {
                toUpsert.Add(p);
                continue;
            }

            if (NeedsUpdate(p, m, exporter, metadata))
            {
                // Products without a mapped category go to the configured "smistare" fallback.
                if (p.MagentoCategoryId == null)
                {
                    var x = metadata!.categories!.FirstOrDefault(a => a.Key.ToLower().EndsWith("smistare"));
                    p.MagentoCategoryId = x.Value;
                }
                toUpsert.Add(p);
            }
            else
                toSkip.Add(p);
        }

        if (toSkip.Count > 0)
            await _exportRepo.ChangeStatusAsync(batchId, toSkip, ExportStatus.Insert);

        if (toUpsert.Count > 0)
            await exporter.ImportProductsAsync(toUpsert, token);

        if (type == TypeRun.Completo)
        {
            // Full imports disable Magento products no longer present in the current Heron feed.
            var mongoSet = mapped.Select(x => x.Aic)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var toDisable = metadata.magentoProducts!
                .Where(m => !mongoSet.Contains(m.Sku))
                .Select(m => m.Sku)
                .ToList();

            if (toDisable.Count > 0)
                await exporter.DisableProductsAsync(toDisable, token);
        }

        if (toUpsert.Count == 0)
            return new List<string>();
        else
            return toUpsert.Select(a => a.Aic).ToList();
    }

    private bool NeedsUpdate(
    ResolvedProduct mongo,
    MagentoSlimProduct magento,
    IMagentoExporter exporter,
    MagentoMetadata metadata)
    {
        // Keep this comparison narrow: only changes that affect Magento product content trigger an upsert.
        if (magento.Price != mongo.Price)
            return true;

        if (!StringEquals(magento.Manufacturer, mongo.Producer))
            return true;

        if (!StringEquals(magento.Supplier, mongo.SupplierCode))
            return true;

        if (!DescriptionEquals(magento, mongo))
            return true;

        // Category comparison is intentionally disabled until category sync is made deterministic.

        return false;
    }

    private static bool StringEquals(string? a, string? b)
    {
        return string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool DescriptionEquals(MagentoSlimProduct m, ResolvedProduct p)
    {
        // Magento may contain either short or long description depending on previous import history.
        var desc = m.Description?.Trim() ?? "";

        return desc == (p.ShortDescription ?? "").Trim() ||
               desc == (p.LongDescription ?? "").Trim();
    }

    private async Task<List<InventoryItem>>? HandleStockUpdate(
    MagentoMetadata magentoMetadata,
    List<ResolvedProduct> mapped,
    IMagentoExporter exporter,
    string batchId,
    CancellationToken token)
    {
        // Stock update compares current Magento values with resolved availability before sending bulk changes.
        var magentoProducts = magentoMetadata.magentoProducts;
        var magentoDict = magentoProducts!.ToDictionary(x => x.Sku, x => x);

        var processed = 0;

        var statusItems =
            new ConcurrentBag<InventoryItem>();

        var toUpdate =
            new ConcurrentBag<ResolvedProduct>();


        // Start dashboard progress for the stock update phase.
        await _importToMagento.UpdateImportStatusAsync(batchId, totalProductsToUpdate: mapped.Count, updateProductsStatus: OperationsStatus.Running);


        await Parallel.ForEachAsync(
            mapped,
            new ParallelOptions
            {
                MaxDegreeOfParallelism =
                    Environment.ProcessorCount
            },
            async (i, ct) =>
            {
                Interlocked.Increment(
                    ref processed
                );

                // Skip SKUs that are not present in Magento.
                if (!magentoDict.TryGetValue(
                        i.Aic,
                        out var product))
                {
                    return;
                }

                // Products already aligned are only marked in export status.
                if (product.Qty == i.Availability)
                {
                    statusItems.Add(
                        new InventoryItem()
                        {
                            Id = batchId,
                            Sku = i.Aic,
                            Qty = i.Availability
                        });

                    return;
                }

                // Products with different availability are sent to Magento bulk stock update.
                toUpdate.Add(i);

                // Flush export status in chunks to avoid very large Mongo updates.
                if (
                    statusItems.Count >= 200
                )
                {
                    List<InventoryItem> chunk;

                    lock (statusItems)
                    {
                        if (
                            statusItems.Count < 200
                        )
                        {
                            return;
                        }

                        chunk =
                            statusItems
                                .Take(200)
                                .ToList();

                        foreach (var x in chunk)
                        {
                            statusItems.TryTake(
                                out _
                            );
                        }
                    }

                    await _exportRepo
                        .SetStatusBulkAsync(
                            chunk,
                            ExportStatus.UpdatePrice
                        );

                    // Update dashboard progress with the flushed chunk size.
                    await _importToMagento.UpdateImportStatusAsync(batchId, totalProductsUpdated: chunk.Count);

                }
            });

        var items = toUpdate.Select(p => new InventoryItem
        {
            Id = batchId,
            Sku = p.Aic,
            Qty = p.Availability
        }).ToList();


        await exporter.UpdateStockBulkAsync(items, batchId, token);

        return items;
    }

}
