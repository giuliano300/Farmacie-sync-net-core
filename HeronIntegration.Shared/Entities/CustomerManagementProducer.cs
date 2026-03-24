using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class CustomerManagementProducer
{
    [BsonId]
    public string? Id { get; set; }

    [BsonElement("customerId")]
    public string? CustomerId { get; set; }

    [BsonElement("producer")]
    public string Producer { get; set; }

    [BsonElement("createdAt")]
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
}