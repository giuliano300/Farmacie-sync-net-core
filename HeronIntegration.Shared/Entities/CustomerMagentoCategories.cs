using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class CustomerMagentoCategories
{
    [BsonId]
    public string Id { get; set; } // es: customerId_magentoId

    [BsonElement("customerId")]
    public string CustomerId { get; set; }

    [BsonElement("magentoCategoryId")]
    public int MagentoCategoryId { get; set; }

    [BsonElement("parentId")]
    public int ParentId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; }

    [BsonElement("path")]
    public string Path { get; set; }

    [BsonElement("level")]
    public int Level { get; set; }

    [BsonElement("position")]
    public int Position { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; }

    [BsonElement("productCount")]
    public int ProductCount { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
