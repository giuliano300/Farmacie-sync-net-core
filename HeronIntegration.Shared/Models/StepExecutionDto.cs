using HeronIntegration.Shared.Enums;

namespace HeronIntegration.Shared.Models;

public class StepExecutionDto
{
    public string Id { get; set; } = default!;

    public string BatchId { get; set; } = default!;

    public string Step { get; set; } = default!;
    // "HeronImport" | "Farmadati" | "Suppliers" | "Magento"

    public StepStatus Status { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public int AttemptCount { get; set; }

    public bool ManualTrigger { get; set; }

    public string? ErrorMessage { get; set; }
}
