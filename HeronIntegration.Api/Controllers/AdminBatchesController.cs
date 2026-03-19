using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.StepProcessors;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SharpCompress.Common;

[ApiController]
[Route("api/admin/batches")]
public class BatchController : ControllerBase
{
    private readonly IBatchRepository _batchRepo;
    private readonly IStepRepository _stepRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IHostEnvironment _env;
    private readonly IBatchFinalizerService _batchFinalizer;
    private readonly HeronImportStepProcessor _heronProcessor;
    private readonly FarmadatiEnrichmentStepProcessor _farmadatiProcessor;
    private readonly SupplierResolutionStepProcessor _supplierProcessor;
    private readonly MagentoExportStepProcessor _magentoProcessor;
    private readonly BatchProcessManager _processManager;

    public BatchController(
        IBatchRepository batchRepo,
        IStepRepository stepRepo,
        IHostEnvironment env,
        ICustomerRepository customerRepo, 
        HeronImportStepProcessor heronProcessor,
        FarmadatiEnrichmentStepProcessor farmadatiProcessor,
        SupplierResolutionStepProcessor supplierProcessor,
        MagentoExportStepProcessor magentoProcessor,
        BatchProcessManager processManager,
        IBatchFinalizerService batchFinalizer)
    {
        _batchRepo = batchRepo;
        _stepRepo = stepRepo;
        _env = env;
        _customerRepo = customerRepo;
        _heronProcessor = heronProcessor;
        _farmadatiProcessor = farmadatiProcessor;
        _supplierProcessor = supplierProcessor;
        _magentoProcessor = magentoProcessor;
        _processManager = processManager;
        _batchFinalizer = batchFinalizer;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
        => Ok(await _batchRepo.GetLastAsync(100));

    [HttpGet("{batchId}/steps")]
    public async Task<IActionResult> GetSteps(string batchId)
        => Ok(await _stepRepo.GetByBatchAsync(batchId));

    [HttpPost("create")]
    public async Task<ActionResult> Create(string customerId)
    {
        try
        {
            var customer = await _customerRepo.GetByIdAsync(customerId);
            if (customer == null)
                return StatusCode(500, new
                {
                    error = "Utente non trovato"
                });

            var root = _env.ContentRootPath;
            var parent = Directory.GetParent(root)!.FullName;

            var folder = Path.Combine(
                parent,
                "HeronFolder",
                customer.HeronFolder
            );
            if (!Directory.Exists(folder))
                return StatusCode(500, new
                {
                    error = "Folder non trovato : " + folder
                });

            var pathFile = Directory.GetFiles(folder).FirstOrDefault();
            if (pathFile == null)
                return StatusCode(500, new
                {
                    error = "Path file non trovato : " + pathFile
                });

            var seq = await _batchRepo.GetNextSequenceAsync(customerId);
            var batch = new BatchExecution
            {
                Id = ObjectId.GenerateNewId(),
                CustomerId = customerId,
                SequenceNumber = seq,
                StartedAt = DateTime.UtcNow,
                Status = BatchStatus.Running,
                TriggeredBy = "Admin",
                HeronFileName = Path.GetFileName(pathFile),
                HeronFilePath = pathFile
            };

            var batchId = await _batchRepo.CreateAsync(batch);

            await _stepRepo.CreateDefaultStepsAsync(batchId, customerId);

            return Ok(new { batchId });
        }
        catch(Exception e)
        {
            return StatusCode(500, new
            {
                error = e.Message,
                stack = e.StackTrace
            });
        }
    }

    [HttpPost("{batchId}/{stepId}/restart")]
    public async Task<bool> Restart(string batchId, string stepId)
    {
        try
        {
            var token = _processManager.Start(ProcessType.Batch,batchId);

            await _stepRepo.ResetStepsAsync(batchId);
            var step = await _stepRepo.GetByIdAsync(stepId);
            switch (step!.Step.ToUpper())
            {
                case "HERONIMPORT":
                    await _heronProcessor.ExecuteAsync(batchId, token);
                    break;
                case "FARMADATI":
                    await _farmadatiProcessor.ExecuteAsync(batchId, token);
                    break;
                case "SUPPLIERS":
                    await _supplierProcessor.ExecuteAsync(batchId, token);
                    break;
                case "MAGENTO":
                    await _magentoProcessor.ExecuteAsync(batchId, token);
                    break;
            }
            await _batchRepo.SetRunningAsync(batchId);
            return true;
        }
        catch (Exception e)
        {

        }

        return false;
    }

    [HttpPost("{batchId}/start")]
    public async Task<bool> start(string batchId)
    {
        try
        {
            var token = _processManager.Start(ProcessType.Batch, batchId);

            await _batchRepo.SetRunningAsync(batchId);
            await _heronProcessor.ExecuteAsync(batchId, token);
            return true;
        }
        catch(Exception e)
        {

        }

        return false;
    }

    [HttpGet("status/{customerId}")]
    public async Task<BatchStatusResponse> GetStatus(string customerId)
    {
        try
        {
            var runningBatch = await _batchRepo.GetRunningBatchAsync(customerId);

            if (runningBatch == null)
            {
                return new BatchStatusResponse
                {
                    CanStartNewBatch = true
                };
            }

            var steps = await _stepRepo.GetByBatchAsync(runningBatch.Id.ToString());

            var currentStep = steps
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefault();

            return new BatchStatusResponse
            {
                CanStartNewBatch = false,
                RunningBatchId = runningBatch.Id.ToString(),
                CurrentStep = currentStep?.Step,
                StepStatus = currentStep?.Status
            };
        }
        catch (Exception e)
        {
            return new BatchStatusResponse
            {
                CanStartNewBatch = false,
                RunningBatchId = null,
                CurrentStep = e.Message.ToString(),
                StepStatus = null
            };
        }

    }

    [HttpGet("finalize-batch")]
    public async Task<bool> Finalize(DateTime? y = null)
    {
        try
        {
            var runningBatch = await _batchRepo.GetOpenBatchesAsync(y);

            foreach (var batch in runningBatch)
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

            return true;
        }
        catch (Exception e)
        {
            return false;
        }

    }
}
