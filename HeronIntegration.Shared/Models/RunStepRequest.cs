using HeronIntegration.Shared.Enums;

namespace HeronIntegration.Shared.Models;

public class RunStepRequest
{
    public string Step { get; set; } = default!;
    public string BatchId { get; set; } = default!;
}
