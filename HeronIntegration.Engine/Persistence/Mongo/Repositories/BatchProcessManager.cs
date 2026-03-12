using System.Collections.Concurrent;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public class BatchProcessManager
    {
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _running =
            new();

        public CancellationToken Start(string batchId)
        {
            Stop(batchId);

            var cts = new CancellationTokenSource();
            _running[batchId] = cts;

            return cts.Token;
        }

        public void Stop(string batchId)
        {
            if (_running.TryRemove(batchId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        public bool IsRunning(string batchId)
        {
            return _running.ContainsKey(batchId);
        }
    }
}
