using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using Microsoft.AspNetCore.Mvc;

namespace HeronIntegration.Engine.Api.Controllers;

[ApiController]
[Route("api/Producer-mappings")]
public class ProducerMappingsController : ControllerBase
{
    private readonly ProducerMappingRepository _repo;

    public ProducerMappingsController(ProducerMappingRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string customerId)
    {
        var list = await _repo.GetByCustomerAsync(customerId);
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var item = await _repo.GetByIdAsync(id);

        if (item == null)
            return NotFound();

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProducerMapping Producer)
    {
        await _repo.CreateAsync(Producer);
        return Ok(Producer);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ProducerMapping Producer)
    {
        await _repo.UpdateAsync(id, Producer);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await _repo.DeleteAsync(id);
        return Ok();
    }
}