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

    public string? Producer { get; set; }

    public string Aic { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? ShortDescription { get; set; }

    public string? LongDescription { get; set; }

    public string? Atc { get; set; }
    public string? Source { get; set; }
    public decimal HeronPrice { get; set; }
    public int HeronStock { get; set; }


    public List<ProductImage> Images { get; set; } = new();

    public DateTime CachedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public static EnrichedProduct CreateMinimal(RawProduct raw, string batchId)
    {
        return new EnrichedProduct
        {
            BatchId = ObjectId.Parse(batchId),
            CustomerId = raw.CustomerId,
            Aic = raw.Aic,

            Name = raw.Name!,
            ShortDescription = raw.Name,
            LongDescription = null,

            Category = raw.Category,
            SubCategory = raw.SubCategory,
            Producer = raw.Producer,

            Images = new List<ProductImage>(),

            HeronPrice = raw.Price,
            HeronStock = raw.Stock,

            CachedAt = DateTime.UtcNow,

            CreatedAt = DateTime.UtcNow
        };
    }

        public static EnrichedProduct FromCache(
        RawProduct raw,
        FarmadatiCache cache,
        string batchId)
        {
            return new EnrichedProduct
            {
                Id = ObjectId.GenerateNewId(),
                BatchId = ObjectId.Parse(batchId),
                CustomerId = raw.CustomerId,
                Aic = raw.Aic,

                Name = cache.Name,
                ShortDescription = cache.ShortDescription,
                LongDescription = cache.LongDescription,

                Category = raw.Category,
                SubCategory = raw.SubCategory,
                Producer = raw.Producer,

                Images = cache.Images ?? new List<ProductImage>(),
                HeronPrice = raw.Price,
                HeronStock = raw.Stock,
                CreatedAt = DateTime.UtcNow,
                Source = "CACHE"
            };
        }

}
