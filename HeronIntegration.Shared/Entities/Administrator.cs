using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Entities
{
    public class Administrator
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("businessName")]
        public string businessName { get; set; } = null!;

        [BsonElement("username")]
        public string username { get; set; } = null!;

        [BsonElement("email")]
        public string email { get; set; }

        [BsonElement("pwd")]
        public string pwd { get; set; }

        [BsonElement("lastLogin")]
        public DateTime? lastLogin { get; set; }

    }
}
