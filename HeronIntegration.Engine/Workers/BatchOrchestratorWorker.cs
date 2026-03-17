using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;

namespace HeronIntegration.Engine.Workers;

public class BatchOrchestratorWorker : BackgroundService
{
    private readonly ILogger<BatchOrchestratorWorker> _logger;
    private readonly IBatchRepository _batchRepo;
    private readonly IStepRepository _stepRepo;
    private readonly IEnumerable<IStepProcessor> _processors;
    private readonly IBatchFinalizerService _batchFinalizer;
    private readonly IMagentoExporterFactory _magentoExporterFactory;
    private readonly ICustomerRepository _customerRepo;
    private readonly BatchProcessManager _processManager;


    public BatchOrchestratorWorker(
        ILogger<BatchOrchestratorWorker> logger,
        IBatchRepository batchRepo,
        IStepRepository stepRepo,
        IEnumerable<IStepProcessor> processors,
        IBatchFinalizerService batchFinalizer,
        IMagentoExporterFactory magentoExporterFactory,
        ICustomerRepository customerRepo,
        BatchProcessManager processManager
    )
    {
        _logger = logger;
        _batchRepo = batchRepo;
        _stepRepo = stepRepo;
        _processors = processors;
        _batchFinalizer = batchFinalizer;
        _magentoExporterFactory = magentoExporterFactory;
        _customerRepo = customerRepo;
        _processManager = processManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Batch Orchestrator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunPipeline(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task RunPipeline(CancellationToken token)
    {
        var runningBatches = await _batchRepo.GetRunningAsync();

        foreach (var batch in runningBatches)
        {
            var nextStep = await _stepRepo.GetNextPendingStepAsync(batch.Id.ToString());

            if (nextStep == null)
            {
                await FinalizeBatch(batch.Id.ToString(), token);
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
            var token = _processManager.Start(ProcessType.Batch, step.BatchId.ToString());

            var processor = _processors
                .FirstOrDefault(p => p.Step == step.Step);

            if (processor == null)
                throw new Exception($"Processor non trovato per step {step.Step}");

            await processor.ExecuteAsync(step.BatchId.ToString(), token);

            await _stepRepo.SetSuccessAsync(step.Id.ToString(), DateTime.UtcNow);
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

    private async Task FinalizeBatch(string batchId, CancellationToken token)
    {
        var batch = await _batchRepo.GetByIdAsync(batchId);
        var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId);

        var exporter = _magentoExporterFactory.Create(customer!.Magento!);

        await exporter.RunMagentoCronAsync(token);

        await _batchFinalizer.FinalizeBatchAsync(batchId);

        _logger.LogInformation("Batch {BatchId} chiuso", batchId);
    }
}
