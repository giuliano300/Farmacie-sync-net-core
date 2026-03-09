using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using Microsoft.AspNetCore.Mvc;

namespace HeronIntegration.Engine.Api.Controllers;

[ApiController]
[Route("api/category-mappings")]
public class CategoryMappingsController : ControllerBase
{
    private readonly CategoryMappingRepository _repo;

    public CategoryMappingsController(CategoryMappingRepository repo)
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
    public async Task<IActionResult> Create([FromBody] CategoryMapping category)
    {
        await _repo.CreateAsync(category);
        return Ok(category);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CategoryMapping category)
    {
        await _repo.UpdateAsync(id, category);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await _repo.DeleteAsync(id);
        return Ok();
    }
}