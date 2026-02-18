using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class BatchReportRepository : IBatchReportRepository
{
    private readonly MongoContext _context;

    public BatchReportRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task InsertOneAsync(BatchReport report)
    {
        await _context.BatchReports.InsertOneAsync(report);
    }

}
