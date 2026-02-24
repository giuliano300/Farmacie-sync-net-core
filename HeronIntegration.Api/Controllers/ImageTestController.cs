using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/test-images")]
public class ImageTestController : ControllerBase
{
    private readonly IProductImageProvider _imageService;
    private readonly IMagentoExporter _exporter;
    private readonly IResolvedProductRepository _repo;

    public ImageTestController(IProductImageProvider imageService, IMagentoExporter magentoExporter, IResolvedProductRepository repo)
    {
        _imageService = imageService;
        _exporter = magentoExporter;
        _repo = repo;
    }

    [HttpGet("")]
    public async Task<IActionResult> Search([FromQuery] string id)
    {
        var p = await _repo.GetById(id);
        var result = await _exporter.UploadImagesAsync(p);

        if (result == null)
            return NotFound();

        await _exporter.RunMagentoCronAsync();


        return Ok(result);
    }
}
