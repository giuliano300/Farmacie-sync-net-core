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
        ICustomerRepository customerRepo)
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
    }

    public async Task<StepExecutionResult> ExecuteAsync(string batchId, CancellationToken token, TypeRun? type = null)
    {
        var result = new StepExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            await _cleanupService.updateExportExecution(batchId);

            var step = await _stepRepo.GetStepAsync(batchId, "HeronImport");
            if (step == null)
            {
                result.ErrorMessage = "Nessun step trovato";
                return result;
            }

            await _stepRepo.SetRunningAsync(step.Id.ToString());


            var batch = await _batchRepo.GetByIdAsync(batchId);
            if (batch == null)
                throw new Exception($"Batch {batchId} non trovato");

            var customer = await _customerRepo.GetByIdAsync(batch.CustomerId);
            if (customer == null)
                throw new Exception($"Customer {batch.CustomerId} non trovato");

            //SCARICA L'ULTIMO FILE DA HERON
            var ftp = new FtpClient(customer.HeronFtp, customer.HeronUsername, customer.HeronPassword);

            ftp.Connect();

            // 👉 prendi il più recente
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

                // assicura che la cartella esista
                if (Directory.Exists(destinationPath))
                {
                    Directory.Delete(destinationPath, true);
                }

                Directory.CreateDirectory(destinationPath!);

                using var ms = new MemoryStream();

                ftp.DownloadStream(ms, latestZip.FullName);
                ms.Position = 0;

                using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

                // prende il file XML dentro lo zip
                var entry = archive.Entries
                    .First(e => !string.IsNullOrEmpty(e.Name) && e.Name.EndsWith(".xml"));

                var destinationFile = Path.Combine(destinationPath!, fileName);

                // scrittura file
                using var entryStream = entry.Open();
                using var fileStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write);

                entryStream.CopyTo(fileStream);
            }
            //////////////////////////

            var parsed = _parser.Parse(batch.HeronFilePath!, batch.CustomerId).ToList();

            var rawProducts = new List<RawProduct>();
            var exportRows = new List<ExportExecution>();

            var categoryMap = await _categoryResolver.LoadMappingsAsync(batch.CustomerId);
            var producerMap = await _producerResolver.LoadMappingsAsync(batch.CustomerId);

            var productToExclude = (await _productToExcludeRepository.GetByCustomerAsync(batch.CustomerId)).Select(a => a.Aic);

            foreach (var p in parsed)
            {
                //NON AGGIUNGE PRODOTTI NON VENDIBILI
                if (productToExclude.Contains(p.Aic))
                    continue;

                var key = $"{p.Category}|{p.SubCategory}";

                int? magentoCategoryId = null;

                if (categoryMap.TryGetValue(key, out var mapped))
                {
                    magentoCategoryId = mapped;
                }


                var producer =
                    producerMap.TryGetValue(p.Producer!, out var mappedProducer)
                        ? mappedProducer
                        : p.Producer;

                rawProducts.Add(new RawProduct
                {
                    BatchId = batch.Id,
                    CustomerId = batch.CustomerId,
                    Aic = p.Aic,
                    Name = p.Name,
                    Price = p.Price,
                    OriginalPrice = p.OriginalPrice,
                    Stock = p.Stock,
                    CreatedAt = DateTime.UtcNow,
                    MagentoCategoryId = magentoCategoryId,
                    Producer = producer,
                    Category = p.Category,
                    SubCategory = p.SubCategory
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
        }

        result.FinishedAt = DateTime.UtcNow;

        return result;
    }
}
