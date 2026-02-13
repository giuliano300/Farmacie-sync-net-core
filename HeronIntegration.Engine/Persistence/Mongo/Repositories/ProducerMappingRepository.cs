using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

public class ProducerMappingRepository : IProducerMappingRepository
{
    private readonly MongoContext _context;

    public ProducerMappingRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<ProducerMapping?> FindAsync(
        string customerId,
        string sourceProducer)
    {
        var filter = Builders<ProducerMapping>.Filter.And(
            Builders<ProducerMapping>.Filter.Eq(x => x.CustomerId, customerId),
            Builders<ProducerMapping>.Filter.Eq(x => x.SourceProducer, sourceProducer)
        );

        return await _context.ProducerMappings
            .Find(filter)
            .FirstOrDefaultAsync();
    }

    public async Task<List<ProducerMapping>> GetByCustomerAsync(string customerId)
    {
        var filter = Builders<ProducerMapping>.Filter.Eq(x => x.CustomerId, customerId);

        return await _context.ProducerMappings
            .Find(filter)
            .ToListAsync();
    }
}
