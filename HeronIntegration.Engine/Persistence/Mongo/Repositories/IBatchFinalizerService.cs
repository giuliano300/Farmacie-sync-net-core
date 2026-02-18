using HeronIntegration.Shared.Entities;

public interface IBatchFinalizerService
{
    Task FinalizeBatchAsync(string batchId);
}
