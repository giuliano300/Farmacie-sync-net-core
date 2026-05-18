using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using System.Collections.Concurrent;

public class MagentoExportStepProcessor : IStepProcessor
{
    public string Step => "Magento";

    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly IExportRepository _exportRepo;
    private readonly IBatchRepository _batchRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IBatchFinalizerService _batchFinalizer;
    private readonly IMagentoExporterFactory _magentoExporterFactory;
    private readonly IStepRepository _stepRepo;
    private readonly ICleanupService _cleanupService;

    public MagentoExportStepProcessor(
        IResolvedProductRepository resolvedRepo,
        IExportRepository exportRepo,
        IBatchFinalizerService batchFinalizer,
        IBatchRepository batchRepo,
        ICustomerRepository customerRepo,
        IMagentoExporterFactory magentoExporterFactory,
        IStepRepository stepRepo,
        ICleanupService cleanupService
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
    }

    public async Task<StepExecutionResult> ExecuteAsync(string batchId, CancellationToken token, TypeRun? type = 0)
    {
        var result = new StepExecutionResult { StartedAt = DateTime.UtcNow };

        try
        {
            if (type == null)
                type = TypeRun.Completo;

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

            // 🔹 mapping unico
            var mapped = MapProducts(resolvedList);

            var sku = mapped.Select(a => a.Aic).ToList();

            // 🔹 download prodotti
            var metadata = await exporter.GetMagentoMetadataAsync(batchId, token);
            var magentoSet = metadata.magentoProducts!
                .Select(x => x.Sku)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var skus = new List<string>();
            // 🔹 UPSERT
            if (type is TypeRun.Completo or TypeRun.ImportProdotti)
                skus = await HandleProductUpsert(metadata, mapped, exporter, batchId, token, type);

            // 🔹 STOCK
            if (type is TypeRun.UpdatePrezzi)
                skus = (await HandleStockUpdate(metadata, mapped, exporter, batchId, token)!).Select(a => a.Sku).ToList();

            // 🔹 IMMAGINI
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
                await exporter.ReindexAsync(skus, batchId, token);
                await exporter.WaitReindexAsync(batchId, token);
                await exporter.CleanIndex(token);
                await exporter.CleanCache(token);
            }

            // FINALIZE
            await _batchFinalizer.FinalizeBatchAsync(batchId);
            //await exporter.StopMagentoImportAsync(batchId);

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
            Vat = p.Vat
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

        var magentoDict = metadata.magentoProducts!
            .ToDictionary(x => x.Sku, StringComparer.OrdinalIgnoreCase);

        var mappedList = mapped.Select(p =>
        {
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
                Images = p.Images
            };
        }).ToList();

        var toUpsert = new List<ResolvedProduct>();
        var toSkip = new List<ResolvedProduct>();

        foreach (var p in mappedList)
        {
            if (!magentoDict.TryGetValue(p.Aic, out var m))
            {
                toUpsert.Add(p);
                continue;
            }

            if (NeedsUpdate(p, m, exporter, metadata))
            {
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
            return null;
        else
            return toUpsert.Select(a => a.Aic).ToList();
    }

    private bool NeedsUpdate(
    ResolvedProduct mongo,
    MagentoSlimProduct magento,
    IMagentoExporter exporter,
    MagentoMetadata metadata)
    {
        if (magento.Price != mongo.Price)
            return true;

        if (!StringEquals(magento.Manufacturer, mongo.Producer))
            return true;

        if (!StringEquals(magento.Supplier, mongo.SupplierCode))
            return true;

        if (!DescriptionEquals(magento, mongo))
            return true;

        //var mongoCat = exporter
        //    .ResolveCategoryId(metadata.categories!, mongo.SubCategory, default)
        //    ?.ToString();

        //if (!string.IsNullOrWhiteSpace(mongoCat) &&
        //    !(magento.Categories ?? new List<string>()).Contains(mongoCat))
        //    return true;

        return false;
    }

    private static bool StringEquals(string? a, string? b)
    {
        return string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool DescriptionEquals(MagentoSlimProduct m, ResolvedProduct p)
    {
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
        var magentoProducts = magentoMetadata.magentoProducts;
        var magentoDict = magentoProducts!.ToDictionary(x => x.Sku, x => x);

        var processed = 0;

        var statusItems =
            new ConcurrentBag<InventoryItem>();

        var toUpdate =
            new ConcurrentBag<ResolvedProduct>();

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

                /*
                |--------------------------------------------------------------------------
                | PRODUCT
                |--------------------------------------------------------------------------
                */

                if (!magentoDict.TryGetValue(
                        i.Aic,
                        out var product))
                {
                    return;
                }

                /*
                |--------------------------------------------------------------------------
                | SAME QTY
                |--------------------------------------------------------------------------
                */

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

                /*
                |--------------------------------------------------------------------------
                | TO UPDATE
                |--------------------------------------------------------------------------
                */

                toUpdate.Add(i);

                /*
                |--------------------------------------------------------------------------
                | BULK EVERY 200
                |--------------------------------------------------------------------------
                */

                if (
                    statusItems.Count >= 200
                )
                {
                    /*
                    |--------------------------------------------------------------------------
                    | LOCK
                    |--------------------------------------------------------------------------
                    */

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

                    /*
                    |--------------------------------------------------------------------------
                    | BULK UPDATE
                    |--------------------------------------------------------------------------
                    */

                    await _exportRepo
                        .SetStatusBulkAsync(
                            chunk,
                            ExportStatus.UpdatePrice
                        );

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
