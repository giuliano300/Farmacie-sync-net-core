using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

[ApiController]
[Route("api/magento-test")]
public class MagentoTestController : ControllerBase
{
    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly IMagentoExporter _magentoExporter;


    public MagentoTestController(IResolvedProductRepository resolvedRepo, IMagentoExporter magentoExporter)
    {
        _resolvedRepo = resolvedRepo;
        _magentoExporter = magentoExporter;
    }

    [HttpGet("")]
    public async Task<IActionResult> ImportTomagento(string batchId, string aic)
    {
        var manufacturersTask = _magentoExporter.GetAttributeOptionsAsync("manufacturer");
        var suppliersTask = _magentoExporter.GetAttributeOptionsAsync("supplier");
        var categoriesTask = _magentoExporter.GetCategoryMapAsync();

        await Task.WhenAll(manufacturersTask, suppliersTask, categoriesTask);

        var manufacturers = manufacturersTask.Result
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().Value,
                StringComparer.OrdinalIgnoreCase
            );

        var suppliers = suppliersTask.Result
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().Value,
                StringComparer.OrdinalIgnoreCase
            );

        var categories = new Dictionary<string, int>(
            categoriesTask.Result,
            StringComparer.OrdinalIgnoreCase
        );

        var products = await _resolvedRepo.GetByBatchAsync(batchId);

        var p = products.FirstOrDefault(a => a.Aic == aic);
        if (p == null) 
            return NotFound();

        if (!string.IsNullOrWhiteSpace(p.SupplierCode) &&
            suppliers.TryGetValue(p.SupplierCode.ToLowerInvariant(), out var supplierId))
            p.SupplierCode = supplierId.ToString();
        else
            p.SupplierCode = "0";

        if (!string.IsNullOrWhiteSpace(p.Producer) &&
            manufacturers.TryGetValue(p.Producer.ToLowerInvariant(), out var manufacturerId))
            p.Producer = manufacturerId.ToString();
        else
            p.Producer = "0";

        var categoryId = ResolveCategoryId(categories, p.SubCategory!.ToLowerInvariant());
        if (categoryId != null)
            p.SubCategory = categoryId.ToString();


        var res = await _magentoExporter.ExportAsync(p);
        if(res.Success)
            return Ok();

        return BadRequest();
    }

    private static int? ResolveCategoryId(
        Dictionary<string, int> categoryMap,
        string categoryName)
    {
        var match = categoryMap
            .FirstOrDefault(x =>
                x.Key.EndsWith("/" + categoryName, StringComparison.OrdinalIgnoreCase)
            );

        if (match.Equals(default(KeyValuePair<string, int>)))
            return null;

        return match.Value;
    }

}
