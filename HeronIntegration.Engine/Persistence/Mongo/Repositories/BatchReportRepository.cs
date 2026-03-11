using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
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


    public async Task<BatchReport> GetBatchesAsync(string batchId)
    {
        return await _context.BatchReports
            .Find(a => a.BatchId == batchId)
            .SortByDescending(x => x.FinishedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<BatchReport?> GetByIdAsync(string id)
    {
        return await _context.BatchReports
            .Find(x => x.Id == ObjectId.Parse(id))
            .FirstOrDefaultAsync();
    }

}
