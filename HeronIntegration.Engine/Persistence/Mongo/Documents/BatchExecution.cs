using HeronIntegration.Shared.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Engine.Persistence.Mongo.Documents;

public class BatchExecution
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string CustomerId { get; set; } = default!;

    public int SequenceNumber { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public BatchStatus Status { get; set; }

    public string TriggeredBy { get; set; } = "System";

    public string? TriggerReason { get; set; }

    public string? HeronFileName { get; set; }

    public string? HeronFilePath { get; set; }
}
