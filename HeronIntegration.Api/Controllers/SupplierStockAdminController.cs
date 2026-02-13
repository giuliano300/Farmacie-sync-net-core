using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SharpCompress.Common;

[ApiController]
[Route("api/admin/supplier-stock")]
public class SupplierStockAdminController : ControllerBase
{
    private readonly ISupplierStockProcessor _processor;

    public SupplierStockAdminController(ISupplierStockProcessor processor)
    {
        _processor = processor;
    }

    [HttpPost("download")]
    public async Task<IActionResult> Download(string supplier)
    {
        await _processor.DownloadAsync(supplier);
        return Ok();
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(string supplier)
    {
        await _processor.ImportAsync(supplier);
        return Ok();
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run(string supplier)
    {
        await _processor.RunAsync(supplier);
        return Ok();
    }

    [HttpPost("download-all")]
    public async Task<IActionResult> DownloadAll()
    {
        await _processor.DownloadAllAsync();
        return Ok();
    }

    [HttpPost("import-all")]
    public async Task<IActionResult> ImportAll()
    {
        await _processor.ImportAllAsync();
        return Ok();
    }

    [HttpPost("run-all")]
    public async Task<IActionResult> RunAll()
    {
        await _processor.RunAllAsync();
        return Ok();
    }
}
