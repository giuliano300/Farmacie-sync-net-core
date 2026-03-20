using HeronIntegration.Shared.Enums;

namespace HeronIntegration.Shared.Models;

public class BatchExecutionDto
{
    public string Id { get; set; } = default!;
    public string CustomerId { get; set; } = default!;
    public TypeRun? type { get; set; } = null;
    public DateTime StartedAt { get; set; }
    public BatchStatus Status { get; set; }
    public int SequenceNumber { get; set; }
    public int? totalMagentoProducts { get; set; }
    public int? totalDownloadMagentoProducts { get; set; }
}
