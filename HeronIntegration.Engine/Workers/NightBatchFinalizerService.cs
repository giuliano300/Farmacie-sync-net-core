using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using Microsoft.Extensions.Hosting;

public class NightBatchFinalizerService : BackgroundService
{
    private readonly IBatchRepository _batchRepo;
    private readonly IBatchFinalizerService _batchFinalizer;

    public NightBatchFinalizerService(IBatchRepository batchRepo, IBatchFinalizerService batchFinalizer)
    {
        _batchRepo = batchRepo;
        _batchFinalizer = batchFinalizer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;

            // Esegui alle 02:00 di notte
            var nextRun = DateTime.Today.AddDays(1).AddHours(2);

            var delay = nextRun - now;

            await Task.Delay(delay, stoppingToken);

            await RunJob(stoppingToken);
        }
    }

    private async Task RunJob(CancellationToken token)
    {
        // prendi batch non finalizzati
        var openBatches = await _batchRepo.GetOpenBatchesAsync();

        foreach (var batch in openBatches)
        {
            try
            {
                await _batchFinalizer.FinalizeBatchAsync(batch.Id.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore batch {batch.Id}: {ex.Message}");
            }
        }
    }
}