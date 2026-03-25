using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
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
            Builders<ProducerMapping>.Filter.Eq(x => x.ManagementLabel, sourceProducer)
        );

        return await _context.ProducerMappings
            .Find(filter)
            .FirstOrDefaultAsync();
    }

    public async Task CreateMultipleAsync(string customerId, List<ProducerMappingDto> p)
    {
        var mappings = p.Select(x =>
        {
            return new ProducerMapping
            {
                Id = $"{x.CustomerId}_{x.MagentoValue}_{x.ManagementKey}",

                CustomerId = x.CustomerId,
                ManagementKey = x.ManagementKey,
                MagentoValue = x.MagentoValue,
                MagentoLabel = x.MagentoLabel,
                ManagementLabel = x.ManagementLabel 
            };
        }).ToList();

        await _context.ProducerMappings.InsertManyAsync(mappings);
    }

    public async Task<List<ProducerMapping>> GetByCustomerAsync(string customerId)
    {
        var filter = Builders<ProducerMapping>.Filter.Eq(x => x.CustomerId, customerId);

        return await _context.ProducerMappings
            .Find(filter)
            .ToListAsync();
    }

    public async Task<ProducerMapping?> GetByIdAsync(string id)
    {
        return await _context.ProducerMappings
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(ProducerMapping producer)
    {
        await _context.ProducerMappings.InsertOneAsync(producer);
    }

    public async Task UpdateAsync(string id, ProducerMapping producer)
    {
        await _context.ProducerMappings.ReplaceOneAsync(
            x => x.Id == id,
            producer);
    }

    public async Task DeleteAsync(string id)
    {
        await _context.ProducerMappings.DeleteOneAsync(
            x => x.Id == id);
    }

}
