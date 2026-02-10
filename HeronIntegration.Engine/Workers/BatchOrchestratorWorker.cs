using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;

namespace HeronIntegration.Engine.Workers;

public class BatchOrchestratorWorker : BackgroundService
{
    private readonly ILogger<BatchOrchestratorWorker> _logger;
    private readonly IBatchRepository _batchRepo;
    private readonly IStepRepository _stepRepo;
    private readonly IEnumerable<IStepProcessor> _processors;

    public BatchOrchestratorWorker(
        ILogger<BatchOrchestratorWorker> logger,
        IBatchRepository batchRepo,
        IStepRepository stepRepo,
        IEnumerable<IStepProcessor> processors)
    {
        _logger = logger;
        _batchRepo = batchRepo;
        _stepRepo = stepRepo;
        _processors = processors;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Batch Orchestrator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunPipeline();
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task RunPipeline()
    {
        var runningBatches = await _batchRepo.GetRunningAsync();

        foreach (var batch in runningBatches)
        {
            var nextStep = await _stepRepo.GetNextPendingStepAsync(batch.Id.ToString());

            if (nextStep == null)
            {
                await FinalizeBatch(batch.Id.ToString());
                continue;
            }

            await ExecuteStep(nextStep);
        }
    }

    private async Task ExecuteStep(StepExecution step)
    {
        await _stepRepo.SetRunningAsync(step.Id.ToString());
        var StartedAt = DateTime.UtcNow;
        try
        {
            var processor = _processors
                .FirstOrDefault(p => p.Step == step.Step);

            if (processor == null)
                throw new Exception($"Processor non trovato per step {step.Step}");

            await processor.ExecuteAsync(step.BatchId.ToString());

            await _stepRepo.SetSuccessAsync(step.Id.ToString(), StartedAt, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            await _stepRepo.SetErrorAsync(step.Id.ToString(), ex.Message);

            _logger.LogError(ex,
                "Errore esecuzione step {Step} Batch {Batch}",
                step.Step,
                step.BatchId);
        }
    }

    private async Task FinalizeBatch(string batchId)
    {
        await _batchRepo.CloseAsync(batchId);

        _logger.LogInformation("Batch {BatchId} chiuso", batchId);
    }
}
