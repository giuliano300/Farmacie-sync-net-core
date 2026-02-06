namespace HeronIntegration.Engine.Steps;

public interface IStepProcessor
{
    string StepName { get; }

    Task ExecuteAsync(string batchId);
}
