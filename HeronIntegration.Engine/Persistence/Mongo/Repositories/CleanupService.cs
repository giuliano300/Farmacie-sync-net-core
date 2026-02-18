using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class CleanupService : ICleanupService
{
    private readonly IRawProductRepository _rawRepo;
    private readonly IEnrichedProductRepository _enrichedRepo;
    private readonly IResolvedProductRepository _resolvedRepo;

    public CleanupService(
    IRawProductRepository rawRepo,
    IEnrichedProductRepository enrichedRepo,
    IResolvedProductRepository resolvedRepo)
    {
        _rawRepo = rawRepo;
        _enrichedRepo = enrichedRepo;
        _resolvedRepo = resolvedRepo;
    }

    public async Task CleanupBatchAsync(string batchId)
    {
        await _rawRepo.DeleteByBatchAsync(batchId);
        await _enrichedRepo.DeleteByBatchAsync(batchId);
        await _resolvedRepo.DeleteByBatchAsync(batchId);
    }
}
