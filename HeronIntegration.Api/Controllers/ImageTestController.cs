using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/test-images")]
public class ImageTestController : ControllerBase
{
    private readonly IProductImageProvider _imageService;

    public ImageTestController(IProductImageProvider imageService)
    {
        _imageService = imageService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        var result = await _imageService.GetImagesAsync("", query);

        if (result == null)
            return NotFound();

        return Ok(result);
    }
}
