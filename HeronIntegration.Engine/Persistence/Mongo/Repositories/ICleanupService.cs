using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public interface ICleanupService
    {
        Task CleanupBatchAsync(string batchId);
    }
}
