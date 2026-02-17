using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class ResolvedProduct
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
    public decimal Price { get; set; }
    public int Availability { get; set; }

    public string? SupplierCode { get; set; }

    public List<ProductImage> Images { get; set; } = new();

    public DateTime ResolvedAt { get; set; }


    public static ResolvedProduct MapToResolved(
    EnrichedProduct raw,
    SupplierStock chosen,
    ObjectId batchObjectId)
    {
        return new ResolvedProduct
        {
            Id = ObjectId.GenerateNewId(),
            BatchId = batchObjectId,
            CustomerId = raw.CustomerId,
            Category = raw.Category,
            SubCategory = raw.SubCategory,
            Producer = raw.Producer,
            Aic = raw.Aic,
            Name = raw.Name,
            ShortDescription = raw.ShortDescription,
            LongDescription = raw.LongDescription,
            Atc = raw.Atc,
            Source = raw.Source,
            Price = chosen.Price,
            Availability = chosen.Availability,
            SupplierCode = chosen.SupplierCode,
            Images = raw.Images,
            ResolvedAt = DateTime.UtcNow
        };
    }

}
