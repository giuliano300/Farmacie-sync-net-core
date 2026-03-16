using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;

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

    public async Task<StepExecutionResult> ExecuteAsync(string batchId, CancellationToken token)
    {
        var result = new StepExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            await _cleanupService.updateExportExecution(batchId);

            var step = await _stepRepo.GetStepAsync(batchId, "Magento");
            if (step == null)
            {
                result.ErrorMessage = "Nessun step trovato";
                return result;
            }

            await _stepRepo.SetRunningAsync(step.Id.ToString());

            //CARICAMENTO DATI CUSTOMER
            var batch = await _batchRepo.GetByIdAsync(batchId);
            var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId);

            if (customer?.Magento == null)
                throw new Exception("Magento config mancante");

            var _exporter = _magentoExporterFactory.Create(customer.Magento);


            // =====================================================
            // CARICAMENTO METADATI MAGENTO
            // =====================================================
            var magentoMetatada = await _exporter.GetMagentoMetadataAsync(batchId, token);

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
                    BatchId = p.BatchId,
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
                                   _exporter.ResolveCategoryId(magentoMetatada.categories!, p.SubCategory, token) is int categoryId)
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
                await _exporter.ImportProductsAsync(toUpsert, token);

            // =====================================================
            // DISABILITAZIONE PRODOTTI MANCANTI
            // =====================================================
            if (toDisable.Any())
                await _exporter.DisableProductsAsync(toDisable, token);

            // =====================================================
            //  UPDATE STOCK
            // =====================================================
            await _exporter.UpdateStockBulkAsync(
                mappedList.Select(p => new InventoryItem
                {
                    Id = batchId,
                    Sku = p.Aic,
                    Qty = p.Availability
                })
                .ToList(), token);

            // =====================================================
            //  UPLOAD IMMAGINI SOLO PER UPSERT(AGGIUNTO NELL'INSERIMENTO)
            // =====================================================
            //var all = mappedList;
            //await _exporter.UpdateImageBulkAsync(all, token);

            // =====================================================
            // CRON MAGENTO
            // =====================================================
            await _exporter.RunMagentoCronAsync(token);

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
}
