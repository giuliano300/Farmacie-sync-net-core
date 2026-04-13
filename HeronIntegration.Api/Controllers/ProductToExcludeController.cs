using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace HeronIntegration.Engine.Api.Controllers;

[ApiController]
[Route("api/product-to-exclude")]
public class ProductToExcludeController : ControllerBase
{
    private readonly IProductToExcludeRepository _productRepo;

    public ProductToExcludeController(
        IProductToExcludeRepository productRepo
    )
    {
        _productRepo = productRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string customerId)
    {
        var list = await _productRepo.GetByCustomerAsync(customerId);
        return Ok(list);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var item = await _productRepo.GetByIdAsync(id);

        if (item == null)
            return NotFound();

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductToExclude p)
    {
        await _productRepo.InsertAsync(p);
        return Ok(p);
    }

    [HttpPost]
    [Route("SetMultiple")]
    public async Task<IActionResult> Create([FromBody] List<ProductToExclude> p)
    {
        await _productRepo.InsertManyAsync(p);
        return Ok(p);

    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ProductToExclude p)
    {
        await _productRepo.UpdateAsync(p);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await _productRepo.DeleteAsync(id);
        return Ok();
    }
}