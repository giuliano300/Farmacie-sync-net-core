using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Enums;
using MongoDB.Bson;

namespace HeronIntegration.Engine.Workers;

public class HeronFileWatcherWorker : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IBatchRepository _batchRepo;
    private readonly IStepRepository _stepRepo;
    private readonly ILogger<HeronFileWatcherWorker> _logger;

    public HeronFileWatcherWorker(
        IConfiguration config,
        IBatchRepository batchRepo,
        IStepRepository stepRepo,
        ILogger<HeronFileWatcherWorker> logger)
    {
        _config = config;
        _batchRepo = batchRepo;
        _stepRepo = stepRepo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var incomingRoot = _config["Heron:IncomingRoot"];

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var customerDir in Directory.GetDirectories(incomingRoot))
            {
                var customerId = Path.GetFileName(customerDir);

                foreach (var file in Directory.GetFiles(customerDir, "*.xml"))
                {
                    await CreateBatch(customerId, file);
                }
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task CreateBatch(string customerId, string filePath)
    {
        var running = await _batchRepo.GetRunningBatchAsync(customerId);
        if (running != null)
            return;

        var seq = await _batchRepo.GetNextSequenceAsync(customerId);

        var batch = new BatchExecution
        {
            Id = ObjectId.GenerateNewId(),
            CustomerId = customerId,
            SequenceNumber = seq,
            StartedAt = DateTime.UtcNow,
            Status = BatchStatus.Running,
            TriggeredBy = "System",
            HeronFileName = Path.GetFileName(filePath),
            HeronFilePath = filePath
        };

        await _batchRepo.CreateAsync(batch);

        await CreateSteps(batch);

        MoveToWorking(customerId, filePath);
    }

    private async Task CreateSteps(BatchExecution batch)
    {
        var steps = new[]
        {
            "HeronImport",
            "Farmadati",
            "Suppliers",
            "Magento"
        };

        foreach (var s in steps)
        {
            await _stepRepo.CreateAsync(new StepExecution
            {
                Id = ObjectId.GenerateNewId(),
                BatchId = batch.Id,
                CustomerId = batch.CustomerId,
                Step = s,
                Status = StepStatus.Pending,
                StartedAt = DateTime.UtcNow
            });
        }
    }

    private void MoveToWorking(string customerId, string filePath)
    {
        var workingRoot = _config["Heron:WorkingRoot"];

        var destDir = Path.Combine(workingRoot, customerId);
        Directory.CreateDirectory(destDir);

        var destFile = Path.Combine(destDir, Path.GetFileName(filePath));

        File.Move(filePath, destFile, true);
    }
}
