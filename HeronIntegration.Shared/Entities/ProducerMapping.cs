using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class ProducerMapping
{
    [BsonId]
    public string? Id { get; set; }

    public string CustomerId { get; set; } = null!;

    public string MagentoValue { get; set; } = null!;
    public string MagentoLabel { get; set; } = null!;

    public string ManagementKey { get; set; } = null!;
    public string ManagementLabel { get; set; } = null!;

}
