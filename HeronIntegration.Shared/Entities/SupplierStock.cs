using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class SupplierStock
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string SupplierCode { get; set; } = default!;

    public string Aic { get; set; } = default!;

    public decimal Price { get; set; }

    public int Availability { get; set; }

    public int Priority { get; set; }

    public DateTime ImportedAt { get; set; }
}
