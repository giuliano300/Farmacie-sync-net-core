using HeronIntegration.Shared.Entities;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

[ApiController]
[Route("api/admin/suppliers")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierRepository _repo;
    private readonly SupplierStockProcessor _processor;

    public SuppliersController(ISupplierRepository repo, SupplierStockProcessor processor)
    {
        _repo = repo;
        _processor = processor;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
        => Ok(await _repo.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
        => Ok(await _repo.GetByIdAsync(id));

    [HttpGet("byCode")]
    public async Task<IActionResult> GetByCode(string code)
        => Ok(await _repo.GetByCode(code));

    [HttpGet("sync")]
    public async Task<bool> sync(string code)
    {
        try
        {
            var fileName = await _processor.DownloadAsync(code);
            if (fileName == null || fileName == "")
            {
                await _repo.UpdateLastUpdate(code);
                 return false;
            }

            var res = await _processor.ImportAsync(code);
            if (res)
            {
                await _repo.UpdateLastUpdate(code);
                return true;
            }


            return false;

        }
        catch (Exception e)
        {

        }
        return false;
    }

    [HttpPost]
    public async Task<IActionResult> Create(Supplier supplier)
    {
        supplier.Id = ObjectId.GenerateNewId().ToString();
        await _repo.InsertAsync(supplier);
        return Ok(supplier);
    }

    [HttpPut("{id}")]
    public async Task<bool> Update(string id, Supplier supplier)
    {
        try
        {
            supplier.Id = id;
            await _repo.UpdateAsync(supplier);
            return true;
        }
        catch (Exception e)
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
