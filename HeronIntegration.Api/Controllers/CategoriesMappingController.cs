using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace HeronIntegration.Engine.Api.Controllers;

[ApiController]
[Route("api/category-mappings")]
public class CategoryMappingsController : ControllerBase
{
    private readonly CategoryMappingRepository _repo;
    private readonly IMagentoExporterFactory _magentoExporterFactory;
    private readonly ICustomerRepository _customerRepo;
    private readonly ICustomerMagentoCategoriesRepository _customeMagentoRepo;
    private readonly ICustomerManagementCategoriesRepository _customerManagementRepo;
    private readonly BatchProcessManager _processManager;
    private readonly IHostEnvironment _env;
    private readonly IHeronXmlParser _parser;

    public CategoryMappingsController(
        CategoryMappingRepository repo,
        IMagentoExporterFactory magentoExporterFactory,
        ICustomerRepository customerRepo,
        ICustomerMagentoCategoriesRepository customeMagentoRepo,
        ICustomerManagementCategoriesRepository customerManagementRepo,
        IHostEnvironment env,
        BatchProcessManager processManager,
        IHeronXmlParser parser
    )
    {
        _repo = repo;
        _magentoExporterFactory = magentoExporterFactory;
        _customerRepo = customerRepo;
        _processManager = processManager;
        _customeMagentoRepo = customeMagentoRepo;
        _env = env;
        _parser = parser;
        _customerManagementRepo = customerManagementRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string customerId)
    {
        var list = await _repo.GetByCustomerAsync(customerId);
        return Ok(list);
    }

    [HttpGet("GetMagentoCategories")]
    public async Task<IActionResult> GetMagentoCategories(string customerId)
    {
        var list = await _customeMagentoRepo.GetByCustomerAsync(customerId);
        return Ok(list);
    }

    [HttpGet("GetManagementCategories")]
    public async Task<IActionResult> GetManagementCategories(string customerId)
    {
        var list = await _customerManagementRepo.GetByCustomerAsync(customerId);
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

    [HttpGet("SetMagentoManagementCategories")]
    public async Task<IActionResult> SetMagentoManagementCategories(string customerId)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId);

        if (customer?.Magento == null)
            throw new Exception("Magento config mancante");

        var exporter = _magentoExporterFactory.Create(customer.Magento);
        var token = _processManager.Start(ProcessType.Singoli, "");

        //Import Magento-->Mongo
        var nodes = await exporter.GetCategoryAsync(token);
        var categories = exporter.FlattenCategoriesNodes(nodes, customerId);
        await _customeMagentoRepo.CreateAsync(customerId, categories);

        //Import Heron-->Mongo
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

        var prodotti = _parser.Parse(pathFile, customerId);

        var categoriesHeron = prodotti
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .Select(p =>
            {
                var categoria = p.Category.Trim();
                var sotto = p.SubCategory?.Trim() ?? "";

                var key = $"{categoria}|{sotto}";

                return new CustomerManagementCategories
                {
                    Id = $"{customerId}_{key}",
                    CustomerId = customerId,
                    Category = categoria,
                    SubCategory = sotto,
                    Key = key
                };
            })
            .GroupBy(x => x.Key)
            .Select(g => g.First())
            .ToList();

        await _customerManagementRepo.CreateAsync(customerId, categoriesHeron);

        return Ok(categories);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoryMapping category)
    {
        await _repo.CreateAsync(category);
        return Ok(category);
    }

    [HttpPost]
    [Route("SetMultipleMapping")]
    public async Task<IActionResult> Create([FromQuery] string customerId, [FromBody] List<CategoryMappingDto> categories)
    {
        await _repo.CreateMultipleAsync(customerId, categories);
        return Ok(categories);
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