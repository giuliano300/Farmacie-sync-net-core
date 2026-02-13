using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class ProducerMapping
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string CustomerId { get; set; } = null!;

    public string SourceProducer { get; set; } = null!;
    public string TargetProducer { get; set; } = null!;

}
