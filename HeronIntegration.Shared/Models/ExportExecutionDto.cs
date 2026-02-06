using HeronIntegration.Shared.Enums;

namespace HeronIntegration.Shared.Models;

public class ExportExecutionDto
{
    public string Id { get; set; } = default!;

    public string BatchId { get; set; } = default!;

    public string CustomerId { get; set; } = default!;

    public string Aic { get; set; } = default!;

    public ExportStatus Status { get; set; }
    // Pending | Success | Error

    public int AttemptCount { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public string? ErrorMessage { get; set; }

    public string PayloadHash { get; set; } = default!;
}
