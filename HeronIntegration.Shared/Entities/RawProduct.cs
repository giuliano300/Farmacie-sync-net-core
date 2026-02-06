using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class RawProduct
{
    [BsonId]
    public ObjectId Id { get; set; }

    public ObjectId BatchId { get; set; }

    public string CustomerId { get; set; } = default!;

    public string Aic { get; set; } = default!;

    public string? Name { get; set; }

    public string? Category { get; set; }

    public string? SubCategory { get; set; }

    public decimal Price { get; set; }

    public decimal OriginalPrice { get; set; }

    public int Stock { get; set; }

    public int Vat { get; set; }

    public string? AtcGmp { get; set; }

    public string? Producer { get; set; }

    public bool Published { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
