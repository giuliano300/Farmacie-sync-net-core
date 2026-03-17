using HeronIntegration.Shared.Enums;
using System.Collections.Concurrent;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public class BatchProcessManager
    {
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _running = new();

        private static string BuildKey(ProcessType type, string id)
            => $"{type}:{id}";

        public CancellationToken Start(ProcessType type, string id)
        {
            var key = BuildKey(type, id);
            var newCts = new CancellationTokenSource();

            _running.AddOrUpdate(
                key,
                newCts,
                (_, existing) =>
                {
                    existing.Cancel();
                    existing.Dispose();
                    return newCts;
                });

            return newCts.Token;
        }

        public void Stop(ProcessType type, string id)
        {
            var key = BuildKey(type, id);

            if (_running.TryRemove(key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        public bool IsRunning(ProcessType type, string id)
        {
            return _running.TryGetValue(BuildKey(type, id), out var cts)
                   && !cts.IsCancellationRequested;
        }
    }
}