using HeronIntegration.Shared.Enums;

namespace HeronIntegration.Shared.Models;

public class BatchExecutionDto
{
    public string Id { get; set; } = default!;
    public string CustomerId { get; set; } = default!;
    public DateTime StartedAt { get; set; }
    public BatchStatus Status { get; set; }
    public int SequenceNumber { get; set; }
}
