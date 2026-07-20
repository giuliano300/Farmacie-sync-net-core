using HeronIntegration.Engine.Persistence.Mongo.Repositories;

namespace HeronIntegration.Engine.Workers;

/// <summary>
/// Synchronizes every active database-configured supplier at 01:00 local time using
/// the same download/import path exposed by the administrative API.
/// </summary>
public sealed class SupplierFileImporterWorker : BackgroundService
{
    private static readonly TimeOnly ScheduledTime = new(1, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SupplierFileImporterWorker> _logger;

    public SupplierFileImporterWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SupplierFileImporterWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Supplier synchronization worker started; scheduled at {ScheduledTime}",
            ScheduledTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(GetDelayUntilNextRun(DateTime.Now), stoppingToken);
                await SynchronizeActiveSuppliersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la sincronizzazione schedulata dei supplier");
            }
        }

        _logger.LogInformation("Supplier synchronization worker stopped");
    }

    private async Task SynchronizeActiveSuppliersAsync(CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var supplierRepository = scope.ServiceProvider.GetRequiredService<ISupplierRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<SupplierStockProcessor>();
        var suppliers = await supplierRepository.GetActiveAsync();

        foreach (var supplier in suppliers)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                var fileName = await processor.DownloadAsync(supplier.Code);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    await supplierRepository.UpdateLastUpdate(supplier.Code);
                    _logger.LogWarning(
                        "Supplier {SupplierCode}: nessun file disponibile",
                        supplier.Code);
                    continue;
                }

                if (!await processor.ImportAsync(supplier.Code))
                {
                    _logger.LogError(
                        "Supplier {SupplierCode}: import non completato",
                        supplier.Code);
                    continue;
                }

                await supplierRepository.UpdateLastUpdate(supplier.Code);
                _logger.LogInformation(
                    "Supplier {SupplierCode} sincronizzato in supplier_stock",
                    supplier.Code);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Errore sincronizzazione supplier {SupplierCode}",
                    supplier.Code);
            }
        }
    }

    private static TimeSpan GetDelayUntilNextRun(DateTime now)
    {
        var nextRun = now.Date.Add(ScheduledTime.ToTimeSpan());
        if (nextRun <= now)
            nextRun = nextRun.AddDays(1);

        return nextRun - now;
    }
}
