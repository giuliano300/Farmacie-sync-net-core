using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

[ApiController]
[Route("api/farmadati-import")]
public class FarmadatiController : ControllerBase
{
    private readonly IHeronXmlParser _parser;
    private readonly IFarmadatiCacheRepository _farmadatiCacheRepo;
    private readonly IProductEnrichmentService _enrichmentService;


    public FarmadatiController(IHeronXmlParser parser, 
        IFarmadatiCacheRepository farmadatiCacheRepo,
        IProductEnrichmentService enrichmentService)
    {
        _parser = parser;
        _farmadatiCacheRepo = farmadatiCacheRepo;
        _enrichmentService = enrichmentService;
    }

    [HttpGet("")]
    public async Task<IActionResult> ImportFromFileHeron(string HeronFilePath, string CustomerId)
    {
        var parsed = _parser.Parse(HeronFilePath, CustomerId);
        var batchIdStatic = ObjectId.GenerateNewId().ToString();

        var cacheList = await _farmadatiCacheRepo.GetByAicsAsync(parsed.Select(x => x.Aic));
        var cacheDict = cacheList.ToDictionary(x => x.Aic, x => x);

        foreach (var p in parsed)
        {
            try 
            { 
                var enrichment = await _enrichmentService.EnrichAsync(p.Aic, p.CustomerId, batchIdStatic);
                if (enrichment != null)
                {
                    var cached = new FarmadatiCache
                    {
                        Aic = enrichment.Aic,
                        Name = enrichment.Name,
                        ShortDescription = enrichment.ShortDescription!,
                        LongDescription = enrichment.LongDescription!,
                        Images = enrichment.Images,
                        CachedAt = DateTime.UtcNow
                    };
                    await _farmadatiCacheRepo.InsertAsync(cached);
                }
            }
            catch(Exception e)
            {

            }
        }

        return Ok();
    }
}
