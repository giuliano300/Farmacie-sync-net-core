using MongoDB.Bson;

namespace HeronIntegration.Shared.Entities;

public class ProductMagentoImage
{
    public string? BatchId { get; set; }
    public string? Sku { get; set; }
    public string? LocalPath { get; set; }
}
