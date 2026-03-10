using HeronIntegration.Shared.Enums;
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
        Task CleanupPipeLineAsync(string step, string batchId);

        Task updateExportExecution(string batchId, ExportStatus status = ExportStatus.Pending);
    }
}
