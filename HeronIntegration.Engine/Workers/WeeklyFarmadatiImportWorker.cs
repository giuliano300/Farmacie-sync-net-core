using HeronIntegration.Engine.External.Farmadati.FullImportNew;
using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Singletons;
using MongoDB.Bson;

namespace HeronIntegration.Engine.Workers;

/// <summary>
/// Runs the complete Farmadati import every Sunday at 22:00 local time. Cache and
/// GridFS snapshots provide compensating rollback on the standalone MongoDB server.
/// </summary>
public sealed class WeeklyFarmadatiImportWorker : BackgroundService
{
    private const DayOfWeek ScheduledDay = DayOfWeek.Sunday;
    private static readonly TimeOnly ScheduledTime = new(22, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FarmadatiJobManager _jobManager;
    private readonly MongoCompensationService _compensation;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WeeklyFarmadatiImportWorker> _logger;

    public WeeklyFarmadatiImportWorker(
        IServiceScopeFactory scopeFactory,
        FarmadatiJobManager jobManager,
        MongoCompensationService compensation,
        IConfiguration configuration,
        ILogger<WeeklyFarmadatiImportWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _jobManager = jobManager;
        _compensation = compensation;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Weekly Farmadati full import worker started; scheduled on {ScheduledDay} at {ScheduledTime}",
            ScheduledDay,
            ScheduledTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(GetDelayUntilNextRun(DateTime.Now), stoppingToken);
                await RunImportAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'import settimanale Farmadati");
            }
        }

        _logger.LogInformation("Weekly Farmadati full import worker stopped");
    }

    private async Task RunImportAsync(CancellationToken stoppingToken)
    {
        if (_jobManager.IsRunning)
        {
            _logger.LogWarning("Import Farmadati settimanale saltato: un import è già in esecuzione");
            return;
        }

        using var jobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _jobManager.CancellationTokenSource = jobCancellation;
        var backups = new List<MongoBackup>();

        try
        {
            foreach (var collection in new[] { "farmadati_cache", "fs.files", "fs.chunks" })
            {
                backups.Add(await _compensation.CreateBackupAsync(
                    collection,
                    new BsonDocument(),
                    jobCancellation.Token));
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var job = scope.ServiceProvider.GetRequiredService<IFarmadatiFullImportJob>();

            _logger.LogInformation("Avvio import Farmadati settimanale completo");
            await job.ExecuteAsync(ImportType.Full, jobCancellation.Token);
            _logger.LogInformation("Import Farmadati settimanale completo terminato");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import Farmadati settimanale fallito; avvio rollback");
            try
            {
                await _compensation.RestoreAsync(backups, CancellationToken.None);
                CleanupTemporaryFiles();
            }
            catch (Exception rollbackException)
            {
                _logger.LogCritical(rollbackException, "Rollback import Farmadati fallito");
                throw new AggregateException(ex, rollbackException);
            }

            throw;
        }
        finally
        {
            await _compensation.DropBackupsAsync(backups, CancellationToken.None);
            if (ReferenceEquals(_jobManager.CancellationTokenSource, jobCancellation))
                _jobManager.CancellationTokenSource = null;
        }
    }

    private void CleanupTemporaryFiles()
    {
        try
        {
            var rootPath = _configuration["Farmadati:RootPath"];
            if (string.IsNullOrWhiteSpace(rootPath))
                return;

            var temporaryFolder = Path.Combine(AppContext.BaseDirectory, rootPath);
            if (Directory.Exists(temporaryFolder))
                Directory.Delete(temporaryFolder, recursive: true);

            _logger.LogWarning("Puliti i file temporanei Farmadati dopo il rollback");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Impossibile pulire i file temporanei Farmadati dopo il rollback");
        }
    }

    private static TimeSpan GetDelayUntilNextRun(DateTime now)
    {
        var daysUntilRun = ((int)ScheduledDay - (int)now.DayOfWeek + 7) % 7;
        var nextRun = now.Date
            .AddDays(daysUntilRun)
            .Add(ScheduledTime.ToTimeSpan());

        if (nextRun <= now)
            nextRun = nextRun.AddDays(7);

        return nextRun - now;
    }
}
