using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
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

    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _runningApiProcesses = new();

    public MagentoController(
        IResolvedProductRepository resolvedRepo,
        IExportRepository exportRepo,
        IMagentoExporterFactory magentoExporterFactory,
        IBatchRepository batchRepo,
        ICustomerRepository customerRepo,
        IBatchFinalizerService batchFinalizer,
        ICleanupService cleanupService,
        IStepRepository stepRepo)
    {
        _resolvedRepo = resolvedRepo;
        _exportRepo = exportRepo;
        _customerRepo = customerRepo;
        _magentoExporterFactory = magentoExporterFactory;
        _batchFinalizer = batchFinalizer;
        _batchRepo = batchRepo;
        _cleanupService = cleanupService;
        _stepRepo = stepRepo;
    }

    //--------------------------------------------------
    // STOP PROCESSI PRECEDENTI
    //--------------------------------------------------

    private void CancelRunningProcess(string batchId)
    {
        if (_runningApiProcesses.TryRemove(batchId, out var existing))
            existing.Cancel();
    }

    private CancellationToken StartProcess(string batchId)
    {
        CancelRunningProcess(batchId);

        var cts = new CancellationTokenSource();
        _runningApiProcesses[batchId] = cts;

        return cts.Token;
    }

    //--------------------------------------------------
    // MASSIVE IMPORT
    //--------------------------------------------------

    [HttpGet("")]
    public async Task<IActionResult> MassiveImport(string batchId)
    {
        await _cleanupService.updateExportExecution(batchId);

        var step = await _stepRepo.GetStepAsync(batchId, "Magento");

        if (step == null)
            throw new Exception("Step Magento non trovato");

        await _stepRepo.SetRunningAsync(step.Id.ToString());

        var token = StartProcess(batchId);

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

                var magentoMetadata = await exporter.GetMagentoMetadataAsync();

                var magentoDict = magentoMetadata.magentoProducts!
                    .ToDictionary(x => x.Sku, StringComparer.OrdinalIgnoreCase);

                var resolvedList = await _resolvedRepo.GetByBatchAsync(batchId);

                token.ThrowIfCancellationRequested();

                var mappedList = resolvedList.Select(p => new ResolvedProduct
                {
                    Aic = p.Aic,
                    Name = p.Name,
                    Price = p.Price,
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

                foreach (var product in mappedList)
                {
                    token.ThrowIfCancellationRequested();

                    if (!magentoDict.TryGetValue(product.Aic, out var magentoProduct))
                    {
                        toUpsert.Add(product);
                        continue;
                    }

                    if (magentoProduct.Price != product.Price)
                        toUpsert.Add(product);
                }

                token.ThrowIfCancellationRequested();

                if (toUpsert.Any())
                    await exporter.ImportProductsAsync(toUpsert);

                token.ThrowIfCancellationRequested();

                await UpdateStockBulkInternal(batchId, exporter, token);

                token.ThrowIfCancellationRequested();

                await exporter.UpdateImageBulkAsync(mappedList);

                token.ThrowIfCancellationRequested();

                await exporter.RunMagentoCronAsync();

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

        var token = StartProcess(batchId);

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
        token.ThrowIfCancellationRequested();

        var inventory = await _resolvedRepo.GetByBatchAsync(batchId);

        token.ThrowIfCancellationRequested();

        var list = inventory.Select(p => new InventoryItem
        {
            Id = batchId,
            Sku = p.Aic,
            Qty = p.Availability
        }).ToList();

        await exporter.UpdateStockBulkAsync(list);

        token.ThrowIfCancellationRequested();

        await exporter.RunMagentoCronAsync();
    }

    //--------------------------------------------------
    // UPDATE IMAGES
    //--------------------------------------------------

    [HttpGet("updateImageBulk")]
    public async Task<IActionResult> UpdateImageBulkAsync(string batchId)
    {
        await _cleanupService.updateExportExecution(batchId, ExportStatus.UpdatePrice);

        var token = StartProcess(batchId);

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

                await exporter.UpdateImageBulkAsync(list);

                token.ThrowIfCancellationRequested();

                await exporter.RunMagentoCronAsync();
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
        var batch = await _batchRepo.GetByIdAsync(batchId);
        var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId);

        if (customer?.Magento == null)
            throw new Exception("Magento config mancante");

        var exporter = _magentoExporterFactory.Create(customer.Magento);

        await exporter.RunMagentoCronAsync();

        return Ok("Cron eseguito");
    }

    //--------------------------------------------------
    // FINALIZE
    //--------------------------------------------------

    [HttpGet("finalizeBatchAsync")]
    public async Task<IActionResult> FinalizeBatchAsync(string batchId)
    {
        await _batchFinalizer.FinalizeBatchAsync(batchId);

        return Ok("Batch finalizzato");
    }
}