using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Collections.Concurrent;

[ApiController]
[Route("api/Magento")]
public class MagentoController : ControllerBase
{
    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly IExportRepository _exportRepo;
    private readonly IMagentoExporterFactory _magentoExporterFactory;
    private readonly IBatchFinalizerService _batchFinalizer;
    private readonly ICustomerRepository _customerRepo;
    private readonly IBatchRepository _batchRepo;
    private readonly ICleanupService _cleanupService;
    private readonly IStepRepository _stepRepo;
    private readonly BatchProcessManager _processManager;

    public MagentoController(
        IResolvedProductRepository resolvedRepo,
        IExportRepository exportRepo,
        IMagentoExporterFactory magentoExporterFactory,
        IBatchRepository batchRepo,
        ICustomerRepository customerRepo,
        IBatchFinalizerService batchFinalizer,
        ICleanupService cleanupService,
        IStepRepository stepRepo,
        BatchProcessManager processManager)
    {
        _resolvedRepo = resolvedRepo;
        _exportRepo = exportRepo;
        _customerRepo = customerRepo;
        _magentoExporterFactory = magentoExporterFactory;
        _batchFinalizer = batchFinalizer;
        _batchRepo = batchRepo;
        _cleanupService = cleanupService;
        _stepRepo = stepRepo;
        _processManager = processManager;
    }


    //--------------------------------------------------
    // MASSIVE IMPORT
    //--------------------------------------------------

    [HttpGet("")]
    public async Task<IActionResult> MassiveImport(string batchId)
    {
        await _cleanupService.updateExportExecution(batchId);
        await _exportRepo.SetStatusBatchAsync(batchId, ExportStatus.Pending);


        var step = await _stepRepo.GetStepAsync(batchId, "Magento");

        if (step == null)
            throw new Exception("Step Magento non trovato");

        await _stepRepo.SetRunningAsync(step.Id.ToString());

        var token = _processManager.Start(ProcessType.Batch, batchId);

        _ = Task.Run(async () =>
        {
            try
            {
                var batch = await _batchRepo.GetByIdAsync(batchId);
                var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId);

                if (customer?.Magento == null)
                    throw new Exception("Magento config mancante");

                var exporter = _magentoExporterFactory.Create(customer.Magento);

                token.ThrowIfCancellationRequested();

                var magentoMetadata = await exporter.GetMagentoMetadataAsync(batchId, token);

                var magentoDict = magentoMetadata.magentoProducts!
                    .ToDictionary(x => x.Sku, StringComparer.OrdinalIgnoreCase);

                var resolvedList = await _resolvedRepo.GetByBatchAsync(batchId);

                token.ThrowIfCancellationRequested();

                var mappedList = resolvedList.Select(p => new ResolvedProduct
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
                    SupplierCode = p.SupplierCode,
                    Producer = p.Producer,
                    SubCategory = p.SubCategory,
                    Images = p.Images
                }).ToList();

                var mongoDict = mappedList.ToDictionary(x => x.Aic);

                var toUpsert = new List<ResolvedProduct>();
                var toDisabled = new List<MagentoSlimProduct>();

                foreach (var product in mappedList)
                {
                    token.ThrowIfCancellationRequested();

                    if (!magentoDict.TryGetValue(product.Aic, out var magentoProduct))
                    {
                        toUpsert.Add(product);
                        continue;
                    }

                }
                // prodotti presenti su Magento ma NON nel mapping
                var mappedAics = mappedList.Select(x => x.Aic).ToHashSet();
                foreach (var magentoProduct in magentoDict.Values)
                {
                    token.ThrowIfCancellationRequested();

                    if (!mappedAics.Contains(magentoProduct.Sku))
                    {
                        toDisabled.Add(magentoProduct);
                    }
                }


                token.ThrowIfCancellationRequested();

                if (toUpsert.Any())
                    await exporter.ImportProductsAsync(toUpsert, token);

                if (toDisabled.Any())
                    await exporter.DisableProductsAsync(toDisabled.Select(a => a.Sku).ToList(), token);

                token.ThrowIfCancellationRequested();

                await _exportRepo.SetStatusBatchAsync(batchId, ExportStatus.Insert);

                token.ThrowIfCancellationRequested();

                await UpdateStockBulkInternal(batchId, exporter, token);

                token.ThrowIfCancellationRequested();

                var productWithImages = toUpsert.Where(a => a.Images.Count > 0).ToList();
                if (productWithImages.Count > 0)
                {
                    await exporter.ImportImagesToFtpBulkAsync(productWithImages!, customer, token);
                    await exporter.WaitPollingImagesAsync(batchId, token);
                }

                token.ThrowIfCancellationRequested();


                if(toUpsert.Any())
                {
                    await exporter.ReindexAsync(toUpsert.Select(a => a.Aic).ToList(), batchId, token);
                    await exporter.WaitReindexAsync(batchId, token);
                    await exporter.CleanIndex(token);
                    await exporter.CleanCache(token);
                }

                await _batchFinalizer.FinalizeBatchAsync(batchId);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Batch {batchId} cancellato");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }, token);

        return Ok("Massive import avviato");
    }

    //--------------------------------------------------
    // UPDATE STOCK
    //--------------------------------------------------

    [HttpGet("updateStockBulk")]
    public async Task<IActionResult> UpdateStockBulkAsync(string batchId)
    {
        await _cleanupService.updateExportExecution(batchId, ExportStatus.Insert);

        var token = _processManager.Start(ProcessType.Batch,batchId);

        _ = Task.Run(async () =>
        {
            try
            {
                var batch = await _batchRepo.GetByIdAsync(batchId);
                var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId);

                if (customer?.Magento == null)
                    throw new Exception("Magento config mancante");

                var exporter = _magentoExporterFactory.Create(customer.Magento);

                await UpdateStockBulkInternal(batchId, exporter, token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Stock update cancellato {batchId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }, token);

        return Ok("Update stock avviato");
    }

    private async Task UpdateStockBulkInternal(string batchId, IMagentoExporter exporter, CancellationToken token)
    {
        var inventory = await _resolvedRepo.GetByBatchAsync(batchId);

        token.ThrowIfCancellationRequested();

        var list = inventory.Select(p => new InventoryItem
        {
            Id = batchId,
            Sku = p.Aic,
            Qty = p.Availability
        }).ToList();

        await exporter.UpdateStockBulkAsync(list, batchId, token);

        token.ThrowIfCancellationRequested();

        await exporter.RunMagentoCronAsync(token);
    }

    //--------------------------------------------------
    // UPDATE IMAGES
    //--------------------------------------------------

    [HttpGet("updateImageBulk")]
    public async Task<IActionResult> UpdateImageBulkAsync(string batchId)
    {
        await _cleanupService.updateExportExecution(batchId, ExportStatus.UpdatePrice);

        var token = _processManager.Start(ProcessType.Batch,batchId);

        _ = Task.Run(async () =>
        {
            try
            {
                var batch = await _batchRepo.GetByIdAsync(batchId);
                var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId);

                if (customer?.Magento == null)
                    throw new Exception("Magento config mancante");

                var exporter = _magentoExporterFactory.Create(customer.Magento);

                token.ThrowIfCancellationRequested();

                var list = await _resolvedRepo.GetByBatchAsync(batchId);

                await exporter.UpdateImageBulkAsync(list, token);

                token.ThrowIfCancellationRequested();

                await exporter.RunMagentoCronAsync(token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Upload immagini cancellato {batchId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }, token);

        return Ok("Upload immagini avviato");
    }

    //--------------------------------------------------
    // CRON
    //--------------------------------------------------

    [HttpGet("runMagentoCronAsync")]
    public async Task<IActionResult> RunMagentoCronAsync(string batchId)
    {
        var token = _processManager.Start(ProcessType.Batch,batchId);

        _ = Task.Run(async () =>
        {
            var batch = await _batchRepo.GetByIdAsync(batchId);
            var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId);

            if (customer?.Magento == null)
                throw new Exception("Magento config mancante");

            var exporter = _magentoExporterFactory.Create(customer.Magento);

            await exporter.RunMagentoCronAsync(token);
        }, token);

        return Ok("Cron eseguito");
    }

    //--------------------------------------------------
    // FINALIZE
    //--------------------------------------------------

    [HttpGet("finalizeBatchAsync")]
    public async Task<IActionResult> FinalizeBatchAsync(string batchId)
    {
        var token = _processManager.Start(ProcessType.Batch, batchId);

        _ = Task.Run(async () =>
        {
            await RunMagentoCronAsync(batchId);
            await _batchFinalizer.FinalizeBatchAsync(batchId);
        }, token);

        return Ok("Batch finalizzato");
    }

    //--------------------------------------------------
    // CLEAN INDEX
    //--------------------------------------------------

    [HttpGet("CleanIndex")]
    public async Task<IActionResult> CleanIndex(string batchId)
    {
        var token = _processManager.Start(ProcessType.Batch, batchId);

        var batch = await _batchRepo.GetByIdAsync(batchId);
        var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId);

        if (customer?.Magento == null)
            throw new Exception("Magento config mancante");

        var exporter = _magentoExporterFactory.Create(customer.Magento);
        await exporter.CleanIndex(token);

        return Ok("Indici puliti");
    }

    //--------------------------------------------------
    // CLEAN CACHE
    //--------------------------------------------------

    [HttpGet("CleanCache")]
    public async Task<IActionResult> CleanCache(string batchId)
    {
        var token = _processManager.Start(ProcessType.Batch, batchId);

        var batch = await _batchRepo.GetByIdAsync(batchId);
        var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId);

        if (customer?.Magento == null)
            throw new Exception("Magento config mancante");

        var exporter = _magentoExporterFactory.Create(customer.Magento);
        await exporter.CleanCache(token);

        return Ok("Cache pulita");
    }
    //--------------------------------------------------
    // DELETE PRODUCTS
    //--------------------------------------------------

    [HttpGet("DeleteProducts")]
    public async Task<IActionResult> DeleteProducts(string customerId)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId);

        if (customer?.Magento == null)
            throw new Exception("Magento config mancante");

        var exporter = _magentoExporterFactory.Create(customer.Magento);
        await exporter.DeleteProducts();

        return Ok("Prodotti Eliminati");
    }

    [HttpGet("TestUploadImage")]
    public async Task<IActionResult> TestUploadImage(string customerId, string sku)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId);

        if (customer?.Magento == null)
            throw new Exception("Magento config mancante");

        var exporter = _magentoExporterFactory.Create(customer.Magento);

        var p = new ProductImage()
        {
            GridFsId = ObjectId.Parse("69942f6ee05377b81d2c385c"),
            MimeType = "image/png",
            Type = "gallery",
            AltText = "1.jpg"
        };

        await exporter.UploadImageNewAsync(p, sku, "001", customer, new CancellationToken());

        return Ok("Immagine caricata");
    }
}