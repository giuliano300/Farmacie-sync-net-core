using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HeronIntegration.Engine.Api.Controllers;

[ApiController]
[Route("api/Producer-mappings")]
public class ProducerMappingsController : ControllerBase
{
    private readonly ProducerMappingRepository _repo;
    private readonly IMagentoExporterFactory _magentoExporterFactory;
    private readonly ICustomerRepository _customerRepo;
    private readonly ICustomerMagentoProducerRepository _customeMagentoRepo;
    private readonly ICustomerManagementProducerRepository _customerManagementRepo;
    private readonly BatchProcessManager _processManager;
    private readonly IHostEnvironment _env;
    private readonly IHeronXmlParser _parser;

    public ProducerMappingsController(ProducerMappingRepository repo,
        IMagentoExporterFactory magentoExporterFactory,
        ICustomerMagentoProducerRepository customeMagentoRepo,
        ICustomerManagementProducerRepository customerManagementRepo, 
        IHostEnvironment env,
        BatchProcessManager processManager,
        IHeronXmlParser parser

        )
    {
        _repo = repo;
        _magentoExporterFactory = magentoExporterFactory;
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

    [HttpGet("SetMagentoManagementProducer")]
    public async Task<IActionResult> SetMagentoManagementProducer(string customerId)
    {
        try
        {
            var customer = await _customerRepo.GetByIdAsync(customerId);

            if (customer?.Magento == null)
                throw new Exception("Magento config mancante");

            var exporter = _magentoExporterFactory.Create(customer.Magento);
            var token = _processManager.Start(ProcessType.Singoli, "");

            //Import Magento-->Mongo
            var nodes = await exporter.GetAttributeManufacturerAsync(token);
            var producers = nodes.Select(o => new CustomerMagentoProducer
            {
                Id = $"{customerId}_{o.value}",
                CustomerId = customerId,
                Label = o.label,
                Value = o.value,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await _customeMagentoRepo.CreateAsync(customerId, producers);

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
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

}