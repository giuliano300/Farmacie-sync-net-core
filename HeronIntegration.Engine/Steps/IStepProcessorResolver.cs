using HeronIntegration.Engine.Steps;

public interface IStepProcessorResolver
{
    IStepProcessor Resolve(string step);
}

public class StepProcessorResolver : IStepProcessorResolver
{
    private readonly Dictionary<string, IStepProcessor> _processors;

    public StepProcessorResolver(IEnumerable<IStepProcessor> processors)
    {
        _processors = processors.ToDictionary(x => x.Step, StringComparer.OrdinalIgnoreCase);
    }

    public IStepProcessor Resolve(string step)
    {
        if (!_processors.TryGetValue(step, out var processor))
            throw new Exception($"Processor non trovato per step {step}");

        return processor;
    }
}
