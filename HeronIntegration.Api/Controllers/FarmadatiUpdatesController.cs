using HeronIntegration.Engine.External.Farmadati.FullImportNew;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

[ApiController]
[Route("api/farmadati-updates")]
public class FarmadatiUpdatesController : ControllerBase
{
    private readonly IFarmadatiUpdatesRepository _repo;
    private readonly IFarmadatiFullImportJob _job;
    private readonly BatchProcessManager _processManager;

    public FarmadatiUpdatesController(IFarmadatiUpdatesRepository repo, BatchProcessManager processManager, IFarmadatiFullImportJob job)
    {
        _repo = repo;
        _processManager = processManager;
        _job = job;
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
        try
        {
            await _job.ExecuteAsync();

            return Ok(new
            {
                Success = true,
                Message = "Import Farmadati completato"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                Success = false,
                ex.Message,
                InnerException = ex.InnerException?.Message
            });
        }
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
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }
}
