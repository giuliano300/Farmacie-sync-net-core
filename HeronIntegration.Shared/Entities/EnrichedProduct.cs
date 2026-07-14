using HeronIntegration.Shared.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class EnrichedProduct
{
    [BsonId]
    public ObjectId Id { get; set; }

    public ObjectId BatchId { get; set; }

    public string CustomerId { get; set; } = default!;

    public string? Category { get; set; }

    public string? SubCategory { get; set; }
    public int? MagentoCategoryId { get; set; }

    public string? Producer { get; set; }

    public string Aic { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? ShortDescription { get; set; }

    public string? LongDescription { get; set; }

    public string? Atc { get; set; }
    public string? Source { get; set; }
    public decimal HeronPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal Weight { get; set; }
    public int HeronStock { get; set; }
    public int Vat { get; set; }
    public bool Published { get; set; }
    public string? MacroGroup { get; set; }

    public List<ProductImage> Images { get; set; } = new();

    public DateTime CachedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public static EnrichedProduct CreateMinimal(RawProduct raw, string batchId)
    {
        return ProductMapper.ToMinimalEnriched(raw, batchId);
    }

    public static EnrichedProduct FromCache(
        RawProduct raw,
        FarmadatiCache cache,
        string batchId)
    {
        return ProductMapper.ToEnrichedFromCache(raw, cache, batchId);
    }
}
