using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;

namespace HeronIntegration.Engine.External.Farmadati.Enrichment;

public class ProductEnrichmentService : IProductEnrichmentService
{
    private readonly IProductBaseInfoProvider _baseInfo;
    private readonly IProductLongDescriptionProvider _longDescription;
    private readonly IProductImageProvider _images;

    public ProductEnrichmentService(
        IProductBaseInfoProvider baseInfo,
        IProductLongDescriptionProvider longDescription,
        IProductImageProvider images)
    {
        _baseInfo = baseInfo;
        _longDescription = longDescription;
        _images = images;
    }

    public async Task<EnrichedProduct?> EnrichAsync(string productCode, string customerId, string batchId)
    {
        var baseData = await _baseInfo.GetBaseInfoAsync(productCode);

        if (baseData == null)
            return null;

        var longDesc = await _longDescription.GetLongDescriptionAsync(productCode);
        var imgs = await _images.GetImagesAsync(productCode, baseData.Name);

        return new EnrichedProduct
        {
            Id = ObjectId.GenerateNewId(),
            BatchId = ObjectId.Parse(batchId),
            CustomerId = customerId,
            Aic = baseData.ProductCode,
            Name = baseData.Name,
            ShortDescription = baseData.ShortDescription,
            LongDescription = longDesc,
            Images = imgs.ToList() ?? new List<ProductImage>(),
            CachedAt = DateTime.UtcNow
        };
    }
}
