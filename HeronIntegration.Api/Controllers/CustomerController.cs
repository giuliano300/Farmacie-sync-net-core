using HeronIntegration.Shared.Entities;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

[ApiController]
[Route("api/admin/customers")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerRepository _repo;

    public CustomersController(ICustomerRepository repo)
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
    public async Task<IActionResult> Create(Customer customer)
    {
        customer.Id = ObjectId.GenerateNewId().ToString();
        await _repo.InsertAsync(customer);
        return Ok(customer);
    }

    [HttpPut("{id}")]
    public async Task<bool> Update(string id, Customer customer)
    {
        try
        {
            customer.Id = id;
            await _repo.UpdateAsync(customer);
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
