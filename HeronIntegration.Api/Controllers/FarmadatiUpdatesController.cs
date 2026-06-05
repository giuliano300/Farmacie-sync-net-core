using HeronIntegration.Engine.External.Farmadati.FullImportNew;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using HeronIntegration.Shared.Singletons;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

[ApiController]
[Route("api/farmadati-updates")]
public class FarmadatiUpdatesController : ControllerBase
{
    private readonly IFarmadatiUpdatesRepository _repo;
    private readonly IFarmadatiFullImportJob _job;
    private readonly BatchProcessManager _processManager;
    private readonly FarmadatiJobManager _jobManager;

    public FarmadatiUpdatesController(IFarmadatiUpdatesRepository repo, BatchProcessManager processManager, IFarmadatiFullImportJob job, FarmadatiJobManager jobManager)
    {
        _repo = repo;
        _processManager = processManager;
        _job = job;
        _jobManager = jobManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var updates = await _repo.FindAsync();

        return Ok(updates);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var update = await _repo.GetByIdAsync(id);

        if (update == null)
            return NotFound();

            return Ok(update);
    }

    [HttpPost("import-full")]
    public async Task<IActionResult> ImportFull()
    {
        if (_jobManager.IsRunning)
            return BadRequest("Import già in esecuzione");

        _jobManager.CancellationTokenSource = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            try
            {
                await _job.ExecuteAsync(_jobManager.CancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                   
            }
            finally
            {
                _jobManager.CancellationTokenSource?.Dispose();
                _jobManager.CancellationTokenSource = null;
            }
        });

        return Ok(new
        {
            Success = true,
            Message = "Import Farmadati avviato"
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(FarmadatiUpdates update)
    {
        update.Id = ObjectId.GenerateNewId().ToString();
        update.StartedAt = DateTime.UtcNow;

        var token = _processManager.Start(ProcessType.Farmadati, update.Id);

        token.ThrowIfCancellationRequested();

        await _repo.CreateAsync(update, token);

        return Ok(update);
    }

    [HttpPut("{id}")]
    public async Task<bool> Update(string id, FarmadatiUpdates update)
    {
        try
        {
            update.Id = id;
            await _repo.UpdateAsync(id, update);
            return true;
        }
        catch(Exception e)
        {
            return false;
        }
    }

    [HttpDelete("{id}")]
    public async Task<bool> Delete(string id)
    {
        try
        {
            await _repo.DeleteAsync(id);

            if (_jobManager.IsRunning)
                _jobManager.CancellationTokenSource?.Cancel();
           
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }
}
