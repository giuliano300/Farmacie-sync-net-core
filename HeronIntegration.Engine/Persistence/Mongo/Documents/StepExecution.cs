using HeronIntegration.Shared.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Engine.Persistence.Mongo.Documents;

public class StepExecution
{
    [BsonId]
    public ObjectId Id { get; set; }

    public ObjectId BatchId { get; set; }

    public string CustomerId { get; set; } = default!;

    public string Step { get; set; } = default!;

    public StepStatus Status { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public int AttemptCount { get; set; }

    public bool ManualTrigger { get; set; }

    public string? ErrorMessage { get; set; }
}
