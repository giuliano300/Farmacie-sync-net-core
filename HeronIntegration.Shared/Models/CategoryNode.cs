using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class CategoryNode
    {
        public int Id { get; set; }

        [JsonPropertyName("parent_id")]
        public int ParentId { get; set; }

        public string Name { get; set; }

        public int Level { get; set; }
        public int Position { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }

        [JsonPropertyName("product_count")]
        public int ProductCount { get; set; }

        [JsonPropertyName("children_data")]
        public List<CategoryNode> ChildrenData { get; set; }
    }
}
