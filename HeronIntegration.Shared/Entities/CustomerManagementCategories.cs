using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class CustomerManagementCategories
{
    [BsonId]
    public string Id { get; set; } // es: customerId_Farmaci|SOP

    [BsonElement("customerId")]
    public string CustomerId { get; set; }

    [BsonElement("categoria")]
    public string Category { get; set; }

    [BsonElement("sottoCategoria")]
    public string SubCategory { get; set; }

    [BsonElement("key")]
    public string Key { get; set; } // es: Farmaci|SOP

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}