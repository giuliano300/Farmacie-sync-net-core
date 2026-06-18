using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;

namespace HeronIntegration.Engine.Workers;

public class BatchOrchestratorWorker : BackgroundService
{
    // Keep the orchestrator responsive without hammering MongoDB.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    private readonly ILogger<BatchOrchestratorWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BatchProcessManager _processManager;


    public BatchOrchestratorWorker(
        ILogger<BatchOrchestratorWorker> logger,
        IServiceScopeFactory scopeFactory,
        BatchProcessManager processManager
    )
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _processManager = processManager;
    }

    /// <summary>
    /// Polls running batches and advances each one by a single step per cycle.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Batch Orchestrator started");

        using var timer = new PeriodicTimer(PollInterval);

        try
        {
            do
            {
                // One failed cycle must not kill the hosted service; the next timer tick will retry.
                try
                {
                    await RunPipeline(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore ciclo Batch Orchestrator");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Batch Orchestrator stopped");
        }
    }

    /// <summary>
    /// Loads running batches from MongoDB and executes or finalizes them.
    /// </summary>
    private async Task RunPipeline(CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();

        // Resolve scoped dependencies per cycle so Mongo repositories do not outlive their intended scope.
        var batchRepo = scope.ServiceProvider.GetRequiredService<IBatchRepository>();
        var stepRepo = scope.ServiceProvider.GetRequiredService<IStepRepository>();
        var processors = scope.ServiceProvider.GetRequiredService<IEnumerable<IStepProcessor>>();
        var batchFinalizer = scope.ServiceProvider.GetRequiredService<IBatchFinalizerService>();
        var magentoExporterFactory = scope.ServiceProvider.GetRequiredService<IMagentoExporterFactory>();
        var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

        var runningBatches = await batchRepo.GetRunningAsync();

        foreach (var batch in runningBatches)
        {
            // Batch-level isolation: one broken batch does not block the rest of the queue.
            try
            {
                var nextStep = await stepRepo.GetNextPendingStepAsync(batch.Id.ToString());

                if (nextStep == null)
                {
                    // No pending steps means the import/export pipeline has completed and can be closed.
                    await FinalizeBatch(
                        batch.Id.ToString(),
                        batchRepo,
                        customerRepo,
                        magentoExporterFactory,
                        batchFinalizer,
                        token);
                    continue;
                }

                await ExecuteStep(nextStep, stepRepo, processors);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore orchestrazione batch {BatchId}", batch.Id);
            }
        }
    }

    /// <summary>
    /// Resolves the processor for a step, executes it and persists the final step status.
    /// </summary>
    private async Task ExecuteStep(
        StepExecution step,
        IStepRepository stepRepo,
        IEnumerable<IStepProcessor> processors)
    {
        await stepRepo.SetRunningAsync(step.Id.ToString());
        try
        {
            // The process manager keeps a cancellation token per running batch for manual stop operations.
            var token = _processManager.Start(ProcessType.Batch, step.BatchId.ToString());

            var processor = processors
                .FirstOrDefault(p => p.Step == step.Step);

            if (processor == null)
                throw new Exception($"Processor non trovato per step {step.Step}");

            await processor.ExecuteAsync(step.BatchId.ToString(), token);

            await stepRepo.SetSuccessAsync(step.Id.ToString(), DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            await stepRepo.SetErrorAsync(step.Id.ToString(), ex.Message);

            _logger.LogError(ex,
                "Errore esecuzione step {Step} Batch {Batch}",
                step.Step,
                step.BatchId);
        }
        finally
        {
            // A step execution owns one cancellation slot; always release it when the processor returns.
            _processManager.Stop(ProcessType.Batch, step.BatchId.ToString());
        }
    }

    /// <summary>
    /// Runs Magento cron, saves the final report and closes a batch with no pending steps.
    /// </summary>
    private async Task FinalizeBatch(
        string batchId,
        IBatchRepository batchRepo,
        ICustomerRepository customerRepo,
        IMagentoExporterFactory magentoExporterFactory,
        IBatchFinalizerService batchFinalizer,
        CancellationToken token)
    {
        var batch = await batchRepo.GetByIdAsync(batchId);
        if (batch == null)
        {
            _logger.LogWarning("Batch {BatchId} non trovato in finalizzazione", batchId);
            return;
        }

        var customer = await customerRepo.GetByIdAsync(batch.CustomerId);
        if (customer?.Magento == null)
        {
            _logger.LogWarning(
                "Customer o configurazione Magento mancanti per batch {BatchId}",
                batchId);
            return;
        }

        var exporter = magentoExporterFactory.Create(customer.Magento);

        // Magento cron must run before report cleanup so Magento-side async jobs can complete.
        await exporter.RunMagentoCronAsync(token);

        await batchFinalizer.FinalizeBatchAsync(batchId);

        _logger.LogInformation("Batch {BatchId} chiuso", batchId);
    }
}
