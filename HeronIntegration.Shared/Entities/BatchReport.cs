using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities
{
    public class BatchReport
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public string BatchId { get; set; } = default!;
        public DateTime FinishedAt { get; set; }
        public int TotalProducts { get; set; }
        public int Insert { get; set; }
        public int UpdatePrice { get; set; }
        public int InsertImages { get; set; }
        public int Complete { get; set; }
        public int Errors { get; set; }
    }
}
