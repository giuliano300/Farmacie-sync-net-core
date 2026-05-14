using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;

public interface IBatchReportService
{
    Task SaveBatchReportAsync(BatchReport report, List<ExportExecution> batchExecutions);
}
