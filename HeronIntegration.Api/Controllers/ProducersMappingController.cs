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
        ICustomerRepository customerRep,
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
        _customerRepo = customerRep;
        _processManager = processManager;
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

    [HttpGet("GetMagentoProducer")]
    public async Task<IActionResult> GetMagentoProducer(string customerId)
    {
        var list = await _customeMagentoRepo.GetByCustomerAsync(customerId);
        return Ok(list);
    }

    [HttpGet("GetManagementProducer")]
    public async Task<IActionResult> GetManagementProducer(string customerId)
    {
        var list = await _customerManagementRepo.GetByCustomerAsync(customerId);
        return Ok(list);
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await _repo.DeleteAsync(id);
        return Ok();
    }

    [HttpPost]
    [Route("SetMultipleMapping")]
    public async Task<IActionResult> Create([FromQuery] string customerId, [FromBody] List<ProducerMappingDto> p)
    {
        await _repo.CreateMultipleAsync(customerId, p);
        return Ok(p);

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
            var producers = nodes.Where(a => a.value != "" && a.value != null).Select(o => new CustomerMagentoProducer
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

            var producerHeron =  prodotti
                .Where(p => !string.IsNullOrWhiteSpace(p.Producer))
                .Select(p =>
                {
                    var producer = p.Producer!.Trim();

                    var key = $"{producer}";

                    return new CustomerManagementProducer
                    {
                        Id = $"{customerId}_{key}",
                        CustomerId = customerId,
                        Producer = producer
                    };
                })
                .GroupBy(x => x.Producer)
                .Select(g => g.First())
                .ToList();

            await _customerManagementRepo.CreateAsync(customerId, producerHeron);

            return Ok(prodotti);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

}