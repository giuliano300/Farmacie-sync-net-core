using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class ResolvedProduct
{
    [BsonId]
    public ObjectId Id { get; set; }

    public ObjectId BatchId { get; set; }

    public string CustomerId { get; set; } = default!;

    public string Aic { get; set; } = default!;

    public string SupplierCode { get; set; } = default!;

    public decimal Price { get; set; }

    public int Availability { get; set; }

    public DateTime ResolvedAt { get; set; }
}
