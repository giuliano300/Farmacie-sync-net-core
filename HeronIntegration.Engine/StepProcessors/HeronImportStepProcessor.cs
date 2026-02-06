using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;

namespace HeronIntegration.Engine.StepProcessors;

public class HeronImportStepProcessor : IStepProcessor
{
    public string StepName => "HeronImport";

    private readonly IBatchRepository _batchRepo;
    private readonly IRawProductRepository _rawRepo;
    private readonly IExportRepository _exportRepo;
    private readonly IHeronXmlParser _parser;

    public HeronImportStepProcessor(
        IBatchRepository batchRepo,
        IRawProductRepository rawRepo,
        IExportRepository exportRepo,
        IHeronXmlParser parser)
    {
        _batchRepo = batchRepo;
        _rawRepo = rawRepo;
        _exportRepo = exportRepo;
        _parser = parser;
    }

    public async Task ExecuteAsync(string batchId)
    {
        var batch = await _batchRepo.GetByIdAsync(batchId);
        if (batch == null)
            throw new Exception($"Batch {batchId} non trovato");

        var parsed = _parser.Parse(batch.HeronFilePath!, batch.CustomerId);

        var rawProducts = new List<RawProduct>();
        var exportRows = new List<ExportExecution>();

        foreach (var p in parsed)
        {
            rawProducts.Add(new RawProduct
            {
                BatchId = batch.Id,
                CustomerId = batch.CustomerId,
                Aic = p.Aic,
                Name = p.Name,
                Price = p.Price,
                Stock = p.Stock,
                CreatedAt = DateTime.UtcNow
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

        // 🔴 insert batch
        if (rawProducts.Count > 0)
            await _rawRepo.InsertManyAsync(rawProducts);

        if (exportRows.Count > 0)
            await _exportRepo.InsertManyAsync(exportRows);
    }
}
