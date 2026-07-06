using System.Globalization;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using MongoDB.Bson;

namespace HeronIntegration.Engine.Workers;

public class CustomerCronBatchWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly string[] TimeFormats = ["h\\:mm", "hh\\:mm"];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _env;
    private readonly ILogger<CustomerCronBatchWorker> _logger;

    public CustomerCronBatchWorker(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment env,
        ILogger<CustomerCronBatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _env = env;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Customer cron batch worker started");

        using var timer = new PeriodicTimer(PollInterval);

        try
        {
            do
            {
                try
                {
                    await RunDueCustomerBatches(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore ciclo customer cron batch worker");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Customer cron batch worker stopped");
        }
    }

    private async Task RunDueCustomerBatches(CancellationToken token)
    {
        var now = DateTime.Now;
        var currentTime = new TimeOnly(now.Hour, now.Minute);

        using var scope = _scopeFactory.CreateScope();
        var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var batchRepo = scope.ServiceProvider.GetRequiredService<IBatchRepository>();
        var stepRepo = scope.ServiceProvider.GetRequiredService<IStepRepository>();

        var customers = await customerRepo.GetActiveAsync();

        foreach (var customer in customers)
        {
            token.ThrowIfCancellationRequested();

            if (!IsDueNow(customer, currentTime, out var scheduledTime))
                continue;

            await CreateScheduledBatch(customer, scheduledTime, now.Date, batchRepo, stepRepo);
        }
    }

    private bool IsDueNow(Customer customer, TimeOnly currentTime, out TimeOnly scheduledTime)
    {
        scheduledTime = default;

        if (string.IsNullOrWhiteSpace(customer.Cron))
            return false;

        foreach (var value in customer.Cron.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TimeSpan.TryParseExact(
                    value,
                    TimeFormats,
                    CultureInfo.InvariantCulture,
                    out var parsedTime))
            {
                _logger.LogWarning(
                    "Cron non valido per customer {CustomerId}: {CronValue}",
                    customer.Id,
                    value);
                continue;
            }

            var cronTime = TimeOnly.FromTimeSpan(parsedTime);
            if (cronTime != currentTime)
                continue;

            scheduledTime = cronTime;
            return true;
        }

        return false;
    }

    private async Task CreateScheduledBatch(
        Customer customer,
        TimeOnly scheduledTime,
        DateTime scheduledDate,
        IBatchRepository batchRepo,
        IStepRepository stepRepo)
    {
        var triggerReason = $"CustomerCron:{scheduledDate:yyyy-MM-dd}:{scheduledTime:HH\\:mm}";

        var existingScheduledBatch = await batchRepo.GetByTriggerReasonAsync(customer.Id, triggerReason);
        if (existingScheduledBatch != null)
            return;

        var runningBatch = await batchRepo.GetRunningBatchAsync(customer.Id);
        if (runningBatch != null)
        {
            _logger.LogWarning(
                "Batch cron saltato per customer {CustomerId} alle {ScheduledTime}: batch {BatchId} ancora running",
                customer.Id,
                scheduledTime,
                runningBatch.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(customer.HeronFolder))
        {
            _logger.LogWarning(
                "Batch cron saltato per customer {CustomerId}: HeronFolder mancante",
                customer.Id);
            return;
        }

        var heronFilePath = BuildHeronFilePath(customer);
        Directory.CreateDirectory(Path.GetDirectoryName(heronFilePath)!);

        var seq = await batchRepo.GetNextSequenceAsync(customer.Id);
        var batch = new BatchExecution
        {
            Id = ObjectId.GenerateNewId(),
            CustomerId = customer.Id,
            SequenceNumber = seq,
            StartedAt = DateTime.UtcNow,
            Status = BatchStatus.Running,
            TriggeredBy = "CustomerCron",
            TriggerReason = triggerReason,
            HeronFileName = Path.GetFileName(heronFilePath),
            HeronFilePath = heronFilePath,
            type = TypeRun.Completo
        };

        var batchId = await batchRepo.CreateAsync(batch);
        await stepRepo.CreateDefaultStepsAsync(batchId, customer.Id);

        _logger.LogInformation(
            "Creato batch cron {BatchId} per customer {CustomerId} alle {ScheduledTime}",
            batchId,
            customer.Id,
            scheduledTime);
    }

    private string BuildHeronFilePath(Customer customer)
    {
        var root = _env.ContentRootPath;
        var parent = Directory.GetParent(root)?.FullName ?? root;

        return Path.Combine(
            parent,
            "HeronFolder",
            customer.HeronFolder,
            "heron.xml");
    }
}
