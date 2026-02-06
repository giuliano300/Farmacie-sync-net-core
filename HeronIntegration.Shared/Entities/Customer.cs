using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class Customer
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string Code { get; set; } = default!; // farmacia

    public string Name { get; set; } = default!;

    public string MagentoStoreCode { get; set; } = default!;

    public string HeronFolder { get; set; } = default!;

    public bool Active { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
