using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Enums;
using MongoDB.Bson;

namespace HeronIntegration.Engine.Workers;

public class HeronFileWatcherWorker : BackgroundService
{
    // Missing folders are usually configuration/deploy issues, so retry more slowly.
    private static readonly TimeSpan MissingFolderDelay = TimeSpan.FromSeconds(30);
    // Normal polling is fast because moving an XML should start the batch shortly after arrival.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HeronFileWatcherWorker> _logger;

    public HeronFileWatcherWorker(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<HeronFileWatcherWorker> logger)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Watches the configured incoming root and creates one batch for each customer XML file.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Scan work is separated from the loop so it can be tested and guarded independently.
                await ScanIncomingRoot(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Heron file watcher stopped");
        }
    }

    /// <summary>
    /// Scans all customer folders under the incoming root and processes XML files.
    /// </summary>
    private async Task ScanIncomingRoot(CancellationToken token)
    {
        var incomingRoot = _config["Heron:IncomingRoot"];

        if (string.IsNullOrWhiteSpace(incomingRoot) || !Directory.Exists(incomingRoot))
        {
            // The worker remains alive: once the folder is created, the next scan will pick it up.
            _logger.LogWarning(
                "Heron incoming folder non configurata o non esistente: {IncomingRoot}",
                incomingRoot);

            await Task.Delay(MissingFolderDelay, token);
            return;
        }

        foreach (var customerDir in Directory.GetDirectories(incomingRoot))
        {
            token.ThrowIfCancellationRequested();

            var customerId = Path.GetFileName(customerDir);

            foreach (var file in Directory.GetFiles(customerDir, "*.xml"))
            {
                // File-level isolation: a bad XML or locked file does not block other customer files.
                try
                {
                    await CreateBatch(customerId, file);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Errore creazione batch per customer {CustomerId} e file {File}",
                        customerId,
                        file);
                }
            }
        }
    }

    /// <summary>
    /// Creates a running batch and the default step sequence for one Heron XML file.
    /// </summary>
    private async Task CreateBatch(string customerId, string filePath)
    {
        using var scope = _scopeFactory.CreateScope();
        var batchRepo = scope.ServiceProvider.GetRequiredService<IBatchRepository>();
        var stepRepo = scope.ServiceProvider.GetRequiredService<IStepRepository>();

        var running = await batchRepo.GetRunningBatchAsync(customerId);
        if (running != null)
        {
            // One active batch per customer avoids processing two Heron exports for the same customer concurrently.
            return;
        }

        var seq = await batchRepo.GetNextSequenceAsync(customerId);

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

        await batchRepo.CreateAsync(batch);

        await CreateSteps(batch, stepRepo);

        MoveToWorking(customerId, filePath);
    }

    /// <summary>
    /// Creates the canonical pipeline steps for a new batch.
    /// </summary>
    private async Task CreateSteps(BatchExecution batch, IStepRepository stepRepo)
    {
        var steps = new[]
        {
            // The names must match IStepProcessor.Step values.
            "HeronImport",
            "Farmadati",
            "Suppliers",
            "Magento"
        };

        foreach (var s in steps)
        {
            await stepRepo.CreateAsync(new StepExecution
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

    /// <summary>
    /// Moves a consumed Heron XML file to the working folder for the same customer.
    /// </summary>
    private void MoveToWorking(string customerId, string filePath)
    {
        var workingRoot = _config["Heron:WorkingRoot"];
        if (string.IsNullOrWhiteSpace(workingRoot))
        {
            throw new InvalidOperationException("Configuration value 'Heron:WorkingRoot' is required.");
        }

        var destDir = Path.Combine(workingRoot, customerId);
        Directory.CreateDirectory(destDir);

        var destFile = Path.Combine(destDir, Path.GetFileName(filePath));

        File.Move(filePath, destFile, true);
    }
}
