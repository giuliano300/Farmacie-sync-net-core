using HeronIntegration.Shared.Entities;

public interface IBatchReportService
{
    Task SaveBatchReportAsync(BatchReport report);
}
