using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class ProducerMapping
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string CustomerId { get; set; } = null!;

    public string SourceProducer { get; set; } = null!;
    public string TargetProducer { get; set; } = null!;

}
