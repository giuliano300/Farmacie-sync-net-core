using HeronIntegration.Engine.External.Farmadati.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/test/farmadati")]
public class FarmadatiTestController : ControllerBase
{
    private readonly IProductBaseInfoProvider _provider;
    private readonly IProductLongDescriptionProvider _longProvider;
    private readonly IProductImageProvider _imgProvider;

    public FarmadatiTestController(IProductBaseInfoProvider provider, IProductLongDescriptionProvider longProvider, IProductImageProvider imgProvider)
    {
        _provider = provider;
        _longProvider = longProvider;
        _imgProvider = imgProvider;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetBaseInfo()
    {
        var code = "033262027";
        var result = await _provider.GetBaseInfoAsync(code);
        var description = await _longProvider.GetLongDescriptionAsync(code);
        var img = await _imgProvider.GetImagesAsync(code);

        if (result == null)
            return NotFound($"Prodotto {code} non trovato");

        return Ok(img);
    }
}
