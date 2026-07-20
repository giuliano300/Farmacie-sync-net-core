using HeronIntegration.Engine.Persistence.Mongo.Repositories;

namespace HeronIntegration.Engine.Workers;

/// <summary>
/// Daily retention job. Eligibility starts from batch_report.FinishedAt and cleanup
/// is protected by compensating snapshots for standalone MongoDB.
/// </summary>
public sealed class BatchRetentionWorker : BackgroundService
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BatchRetentionWorker> _logger;

    public BatchRetentionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<BatchRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Batch retention worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(GetDelayUntilNextMidnight(), stoppingToken);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService>();
                var cutoffUtc = DateTime.UtcNow.Subtract(Retention);
                var deletedBatches = await cleanupService.CleanupExpiredBatchesAsync(
                    cutoffUtc,
                    stoppingToken);

                _logger.LogInformation(
                    "Batch retention completed: deleted {DeletedBatches} batches finished before {CutoffUtc}",
                    deletedBatches,
                    cutoffUtc);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la pulizia dei batch scaduti");
            }
        }

        _logger.LogInformation("Batch retention worker stopped");
    }

    private static TimeSpan GetDelayUntilNextMidnight()
    {
        var now = DateTimeOffset.Now;
        var nextMidnight = new DateTimeOffset(DateTime.Today.AddDays(1));

        return nextMidnight - now;
    }
}
