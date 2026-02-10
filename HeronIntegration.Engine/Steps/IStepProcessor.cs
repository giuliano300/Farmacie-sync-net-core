using HeronIntegration.Shared.Models;

namespace HeronIntegration.Engine.Steps;

public interface IStepProcessor
{
    string Step { get; }

    Task<StepExecutionResult> ExecuteAsync(string batchId);
}
