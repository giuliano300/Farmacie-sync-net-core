using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

public class CustomerMagentoProducerRepository : ICustomerMagentoProducerRepository
{
    private readonly MongoContext _context;

    public CustomerMagentoProducerRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<List<CustomerMagentoProducer>> GetByCustomerAsync(string customerId)
    {
        var filter = Builders<CustomerMagentoProducer>.Filter.Eq(x => x.CustomerId, customerId);

        return await _context.CustomerMagentoProducer
            .Find(filter)
            .ToListAsync();
    }

    public async Task<CustomerMagentoProducer?> GetByIdAsync(string id)
    {
        return await _context.CustomerMagentoProducer
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(string customerId, List<CustomerMagentoProducer> Producer)
    {
        // cancella solo quelle del cliente
        await _context.CustomerMagentoProducer.DeleteManyAsync(x => x.CustomerId == customerId);

        await _context.CustomerMagentoProducer.InsertManyAsync(Producer);
    }

    public async Task UpdateAsync(string id, CustomerMagentoProducer category)
    {
        await _context.CustomerMagentoProducer.ReplaceOneAsync(
            x => x.Id ==id,
            category);
    }

    public async Task DeleteAsync(string id)
    {
        await _context.CustomerMagentoProducer.DeleteOneAsync(
            x => x.Id == id);
    }

}
