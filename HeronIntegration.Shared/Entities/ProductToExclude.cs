using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class ProductToExclude
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    public string ProductName { get; set; } = default!;
    public string CustomerId { get; set; } = default!;

    public string Aic { get; set; } = default!;
}
