using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Entities
{
    public class ManagementCache
    {
        public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

        public string Aic { get; set; }

        public DateTime CachedAt { get; set; }

    }
}
