using FluentFTP;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;
using System.IO.Compression;

namespace HeronIntegration.Engine.StepProcessors;

public class HeronImportStepProcessor : IStepProcessor
{
    // Must match the StepExecution.Step value created by HeronFileWatcherWorker.
    public string Step => "HeronImport";

    private readonly IBatchRepository _batchRepo;
    private readonly IRawProductRepository _rawRepo;
    private readonly IExportRepository _exportRepo;
    private readonly IHeronXmlParser _parser;
    private readonly ICategoryResolver _categoryResolver;
    private readonly IProducerResolver _producerResolver;
    private readonly IStepRepository _stepRepo;
    private readonly ICleanupService _cleanupService;
    private readonly IProductToExcludeRepository _productToExcludeRepository;
    private readonly ICustomerRepository _customerRepo;
    private readonly ILogger<HeronImportStepProcessor> _logger;

    public HeronImportStepProcessor(
        IBatchRepository batchRepo,
        IRawProductRepository rawRepo,
        IExportRepository exportRepo,
        IHeronXmlParser parser,
        ICategoryResolver categoryResolver,
        IProducerResolver producerResolver,
        IStepRepository stepRepo,
        ICleanupService cleanupService,
        IProductToExcludeRepository productToExcludeRepository,
        ICustomerRepository customerRepo,
        ILogger<HeronImportStepProcessor> logger)
    {
        _batchRepo = batchRepo;
        _rawRepo = rawRepo;
        _exportRepo = exportRepo;
        _parser = parser;
        _categoryResolver = categoryResolver;
        _producerResolver = producerResolver;
        _stepRepo = stepRepo;
        _cleanupService = cleanupService;
        _productToExcludeRepository = productToExcludeRepository;
        _customerRepo = customerRepo;
        _logger = logger;
    }

    /// <summary>
    /// Imports the Heron XML for a batch, maps categories/producers and creates raw/export rows.
    /// </summary>
    public async Task<StepExecutionResult> ExecuteAsync(string batchId, CancellationToken token, TypeRun? type = null)
    {
        var result = new StepExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Reset export rows from previous attempts before rebuilding this step output.
            await _cleanupService.updateExportExecution(batchId);

            var step = await _stepRepo.GetStepAsync(batchId, "HeronImport");
            if (step == null)
            {
                result.ErrorMessage = "Nessun step trovato";
                return result;
            }

            await _stepRepo.SetRunningAsync(step.Id.ToString());

            // Batch and customer are required because the Heron FTP and pricing rules are customer-specific.
            var batch = await _batchRepo.GetByIdAsync(batchId);
            if (batch == null)
                throw new Exception($"Batch {batchId} non trovato");

            var customer = await _customerRepo.GetByIdAsync(batch.CustomerId);
            if (customer == null)
                throw new Exception($"Customer {batch.CustomerId} non trovato");

            // Download the newest Heron ZIP for this customer before parsing the XML path stored on the batch.
            var ftp = new FtpClient(customer.HeronFtp, customer.HeronUsername, customer.HeronPassword);

            ftp.Connect();

            // Select the newest ZIP file exposed by Heron FTP.
            var files = ftp.GetListing(customer.HeronFtpFolder);

            var latestZip = files
                .Where(x => x.Type == FtpObjectType.File &&
                            x.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Modified)
                .FirstOrDefault();

            if (latestZip != null)
            {

                var destinationPath = Path.GetDirectoryName(batch.HeronFilePath!);
                var fileName = Path.GetFileName(batch.HeronFilePath!);

                // Recreate the destination folder so the parser reads only the current Heron export.
                if (Directory.Exists(destinationPath))
                {
                    Directory.Delete(destinationPath, true);
                }

                Directory.CreateDirectory(destinationPath!);

                using var ms = new MemoryStream();

                ftp.DownloadStream(ms, latestZip.FullName);
                ms.Position = 0;

                using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

                // Heron exports one XML inside the downloaded ZIP.
                var entry = archive.Entries
                    .First(e => !string.IsNullOrEmpty(e.Name) && e.Name.EndsWith(".xml"));

                var destinationFile = Path.Combine(destinationPath!, fileName);

                // Write the XML to the batch path consumed by IHeronXmlParser.
                using var entryStream = entry.Open();
                using var fileStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write);

                entryStream.CopyTo(fileStream);
            }
            // Parse and normalize Heron rows into database entities.
            var parsed = _parser.Parse(batch.HeronFilePath!, batch.CustomerId).ToList();

            var rawProducts = new List<RawProduct>();
            var exportRows = new List<ExportExecution>();

            var categoryMap = await _categoryResolver.LoadMappingsAsync(batch.CustomerId);
            var producerMap = await _producerResolver.LoadMappingsAsync(batch.CustomerId);

            // Customer exclusion list wins before any enrichment or Magento export work.
            var productToExclude = (await _productToExcludeRepository.GetByCustomerAsync(batch.CustomerId)).Select(a => a.Aic);

            foreach (var p in parsed)
            {
                // Skip products explicitly marked as not sellable for this customer.
                if (productToExclude.Contains(p.Aic))
                    continue;

                var key = $"{p.Category}|{p.SubCategory}";

                int? magentoCategoryId = null;

                if (categoryMap.TryGetValue(key, out var mapped))
                {
                    // Category mappings translate Heron category/subcategory into Magento category ids.
                    magentoCategoryId = mapped;
                }


                var producer =
                    producerMap.TryGetValue(p.Producer!, out var mappedProducer)
                        ? mappedProducer
                        : p.Producer;

                // Heron prices may arrive VAT-inclusive depending on customer configuration.
                var price = GetPrice(p.Price, customer.IvaInclusive, p.Vat);
                var originalPrice = GetPrice(p.OriginalPrice, customer.IvaInclusive, p.Vat);

                rawProducts.Add(new RawProduct
                {
                    BatchId = batch.Id,
                    CustomerId = batch.CustomerId,
                    Aic = p.Aic,
                    Name = p.Name,
                    Price = price,
                    OriginalPrice = originalPrice,
                    Stock = p.Stock,
                    CreatedAt = DateTime.UtcNow,
                    MagentoCategoryId = magentoCategoryId,
                    Producer = producer,
                    Category = p.Category,
                    SubCategory = p.SubCategory,
                    Weight = p.Weight,
                    Vat = p.Vat
                });

                exportRows.Add(new ExportExecution
                {
                    Id = ObjectId.GenerateNewId(),
                    BatchId = batch.Id,
                    CustomerId = batch.CustomerId,
                    Aic = p.Aic,
                    Status = Shared.Enums.ExportStatus.Pending,
                    AttemptCount = 0,
                    PayloadHash = Guid.NewGuid().ToString()
                });
            }

            if (rawProducts.Count > 0)
                await _rawRepo.InsertManyAsync(rawProducts);

            if (exportRows.Count > 0)
                await _exportRepo.InsertManyAsync(exportRows);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError($"Step HeronImport errore :" + ex.Message);
        }

        result.FinishedAt = DateTime.UtcNow;

        return result;
    }

    public decimal GetPrice(decimal price, bool ivaInclusive, decimal ivaPercent)
    {
        // Current naming is historical: false means the incoming price must be normalized without VAT.
        if (!ivaInclusive)
        {
            // Nessuna IVA da scorporare
            if (ivaPercent <= 0)
            {
                return price;
            }

            return Math.Round(
                price / (1 + (ivaPercent / 100m)),
                2
            );
        }

        return price;
    }
}
