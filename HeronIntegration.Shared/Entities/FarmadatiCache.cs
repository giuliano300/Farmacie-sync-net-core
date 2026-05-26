using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Entities
{
    public class FarmadatiCache
    {
        public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

        public string Aic { get; set; }

        public string Name { get; set; }

        public string MacroGroup { get; set; }
        public string ShortDescription { get; set; }

        public string LongDescription { get; set; }

        public List<ProductImage> Images { get; set; } = new();

        public DateTime CachedAt { get; set; }

    }
}
