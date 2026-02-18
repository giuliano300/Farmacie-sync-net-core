using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class BatchReportService : IBatchReportService
{
    public async Task SaveBatchReportAsync(BatchReport report)
    {
        Directory.CreateDirectory("batch_reports");

        var file = $"batch_reports/batch_{report.BatchId}.json";

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(file, json);
    }
}
