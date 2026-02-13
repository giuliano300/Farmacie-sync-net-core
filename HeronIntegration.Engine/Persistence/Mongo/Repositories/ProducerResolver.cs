using MongoDB.Bson;
using MongoDB.Driver;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Enums;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class ProducerResolver : IProducerResolver
{
    private readonly IProducerMappingRepository _repo;

    public ProducerResolver(IProducerMappingRepository repo)
    {
        _repo = repo;
    }

    public async Task<string> ResolveAsync(string customerId, string sourceProducer)
    {
        var mapping = await _repo.FindAsync(customerId, sourceProducer);

        return mapping?.TargetProducer ?? sourceProducer;
    }

    public async Task<Dictionary<string, string>> LoadMappingsAsync(string customerId)
    {
        var mappings = await _repo.GetByCustomerAsync(customerId);

        return mappings!.ToDictionary(
            x => x.SourceProducer,
            x => x.TargetProducer
        );
    }
}
