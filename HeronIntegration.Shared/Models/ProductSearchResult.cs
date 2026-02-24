using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeronIntegration.Shared.Models
{
    public class ProductSearchResult
    {
        [JsonPropertyName("items")]
        public List<ProductItem> Items { get; set; }

        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }
    }

    public class ProductItem
    {
        [JsonPropertyName("sku")]
        public string Sku { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("extension_attributes")]
        public JsonElement ExtensionAttributes { get; set; }

        [JsonPropertyName("custom_attributes")]
        public List<CustomAttribute>? CustomAttributes { get; set; } = null;
    }

    public class ExtensionAttributes
    {

        [JsonPropertyName("category_links")]
        public List<CategoryLink> CategoryLinks { get; set; }
    }

    public class StockItem
    {
        [JsonPropertyName("qty")]
        public decimal Qty { get; set; }

        [JsonPropertyName("is_in_stock")]
        public bool IsInStock { get; set; }
    }

    public class CategoryLink
    {
        [JsonPropertyName("category_id")]
        public JsonElement CategoryId { get; set; }
    }

    public class CustomAttribute
    {
        [JsonPropertyName("attribute_code")]
        public string AttributeCode { get; set; }

        [JsonPropertyName("value")]
        public object Value { get; set; }
    }

    public class MagentoSlimProduct
    {
        public string Sku { get; set; }
        public decimal Price { get; set; }
        public decimal Qty { get; set; }
        public bool IsInStock { get; set; }
        public string Manufacturer { get; set; }
        public string Supplier { get; set; }
        public string Description { get; set; }
        public List<string> Categories { get; set; }
    }
}