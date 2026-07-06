using HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class NightBatchFinalizerService : BackgroundService
{
    private static readonly TimeOnly ScheduledTime = new(0, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NightBatchFinalizerService> _logger;

    public NightBatchFinalizerService(
        IServiceScopeFactory scopeFactory,
        ILogger<NightBatchFinalizerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextRun(DateTime.Now);
                _logger.LogInformation(
                    "Night batch finalizer schedulato alle {ScheduledTime}. Prossima esecuzione tra {Delay}",
                    ScheduledTime,
                    delay);

                await Task.Delay(delay, stoppingToken);

                await RunJob(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Night batch finalizer stopped");
        }
    }

    private static TimeSpan GetDelayUntilNextRun(DateTime now)
    {
        var nextRun = now.Date.Add(ScheduledTime.ToTimeSpan());

        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - now;
    }

    private async Task RunJob(CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var batchRepo = scope.ServiceProvider.GetRequiredService<IBatchRepository>();
        var batchFinalizer = scope.ServiceProvider.GetRequiredService<IBatchFinalizerService>();

        var localMidnight = DateTime.Today;
        var cutoffUtc = DateTime.SpecifyKind(localMidnight, DateTimeKind.Local).ToUniversalTime();
        var openBatches = await batchRepo.GetOpenStartedBeforeAsync(cutoffUtc);

        _logger.LogInformation(
            "Night batch finalizer: trovati {Count} batch aperti prima di {CutoffUtc}",
            openBatches.Count,
            cutoffUtc);

        foreach (var batch in openBatches)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                await batchFinalizer.FinalizeBatchAsync(batch.Id.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore finalizzazione batch notturna {BatchId}", batch.Id);
            }
        }
    }
}
