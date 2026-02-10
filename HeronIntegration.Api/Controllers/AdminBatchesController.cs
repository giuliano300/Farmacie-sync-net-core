using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
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

    public BatchController(
        IBatchRepository batchRepo,
        IStepRepository stepRepo)
    {
        _batchRepo = batchRepo;
        _stepRepo = stepRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
        => Ok(await _batchRepo.GetLastAsync(100));

    [HttpGet("{batchId}/steps")]
    public async Task<IActionResult> GetSteps(string batchId)
        => Ok(await _stepRepo.GetByBatchAsync(batchId));

    [HttpPost("create")]
    public async Task<IActionResult> Create(CreateBatchRequest req)
    {
        var seq = await _batchRepo.GetNextSequenceAsync(req.CustomerId);
        var batch = new BatchExecution
        {
            Id = ObjectId.GenerateNewId(),
            CustomerId = req.CustomerId,
            SequenceNumber = seq,
            StartedAt = DateTime.UtcNow,
            Status = BatchStatus.Running,
            TriggeredBy = "Admin",
            HeronFileName = Path.GetFileName(req.HeronFilePath),
            HeronFilePath = req.HeronFilePath
        };

        var batchId = await _batchRepo.CreateAsync(batch);

        await _stepRepo.CreateDefaultStepsAsync(batchId, req.CustomerId);

        return Ok(batchId);
    }

    [HttpPost("{batchId}/restart")]
    public async Task<IActionResult> Restart(string batchId)
    {
        await _stepRepo.ResetStepsAsync(batchId);
        await _batchRepo.SetRunningAsync(batchId);

        return Ok();
    }
}
