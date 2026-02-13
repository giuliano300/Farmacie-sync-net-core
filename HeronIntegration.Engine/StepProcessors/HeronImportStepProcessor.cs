using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;

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

    public HeronImportStepProcessor(
        IBatchRepository batchRepo,
        IRawProductRepository rawRepo,
        IExportRepository exportRepo,
        IHeronXmlParser parser,
        ICategoryResolver categoryResolver,
        IProducerResolver producerResolver)
    {
        _batchRepo = batchRepo;
        _rawRepo = rawRepo;
        _exportRepo = exportRepo;
        _parser = parser;
        _categoryResolver = categoryResolver;
        _producerResolver = producerResolver;
    }

    public async Task<StepExecutionResult> ExecuteAsync(string batchId)
    {
        var result = new StepExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            var batch = await _batchRepo.GetByIdAsync(batchId);
            if (batch == null)
                throw new Exception($"Batch {batchId} non trovato");

            var parsed = _parser.Parse(batch.HeronFilePath!, batch.CustomerId);

            var rawProducts = new List<RawProduct>();
            var exportRows = new List<ExportExecution>();

            var categoryMap = await _categoryResolver.LoadMappingsAsync(batch.CustomerId);
            var producerMap = await _producerResolver.LoadMappingsAsync(batch.CustomerId);

            foreach (var p in parsed)
            {

                var catKey = (p.Category, p.SubCategory);

                var (category, subCategory) =
                    categoryMap.TryGetValue(catKey!, out var mapped)
                        ? mapped
                        : (p.Category, p.SubCategory);

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
                    Stock = p.Stock,
                    CreatedAt = DateTime.UtcNow,
                    Category = category,
                    SubCategory = subCategory,
                    Producer = producer
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
