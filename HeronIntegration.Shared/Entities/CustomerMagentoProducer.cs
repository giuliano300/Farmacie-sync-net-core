using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class CustomerMagentoProducer
{
    [BsonId]
    public string Id { get; set; } // es: customerId_magentoId

    [BsonElement("customerId")]
    public string CustomerId { get; set; }

    [BsonElement("label")]
    public string Label { get; set; }

    [BsonElement("value")]
    public string Value { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
