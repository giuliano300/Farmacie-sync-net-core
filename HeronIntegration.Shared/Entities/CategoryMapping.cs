using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class CategoryMapping
{
    [BsonId]
    public string Id { get; set; }

    public string CustomerId { get; set; }

    // 🔹 SOURCE (gestionale)
    public string SourceCategory { get; set; }
    public string SourceSubCategory { get; set; }

    public string GestionaleKey { get; set; } // "Farmaci|SOP"

    // 🔹 TARGET (magento)
    public int MagentoCategoryId { get; set; }

    // utile per UI (non obbligatorio ma consigliato)
    public string MagentoPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}