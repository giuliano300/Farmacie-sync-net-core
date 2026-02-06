using HeronIntegration.Shared.Entities;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

[ApiController]
[Route("api/admin/suppliers")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierRepository _repo;

    public SuppliersController(ISupplierRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
        => Ok(await _repo.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
        => Ok(await _repo.GetByIdAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create(Supplier supplier)
    {
        supplier.Id = ObjectId.GenerateNewId();
        await _repo.InsertAsync(supplier);
        return Ok(supplier);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, Supplier supplier)
    {
        supplier.Id = ObjectId.Parse(id);
        await _repo.UpdateAsync(supplier);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await _repo.DeleteAsync(id);
        return Ok();
    }
}
