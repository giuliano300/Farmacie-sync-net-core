using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class Login
    {
        [BsonElement("email")]
        public string email { get; set; } = null!;

        [BsonElement("password")]
        public string password { get; set; } = null!;

    }
}
