using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class CategoryMapping
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string CustomerId { get; set; }

    public string SourceCategory { get; set; }
    public string SourceSubCategory { get; set; }

    public string TargetCategory { get; set; }
    public string TargetSubCategory { get; set; }

}
