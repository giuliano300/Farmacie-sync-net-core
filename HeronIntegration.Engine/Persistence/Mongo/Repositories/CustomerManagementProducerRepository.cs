using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

public class CustomerManagementProducerRepository : ICustomerManagementProducerRepository
{
    private readonly MongoContext _context;

    public CustomerManagementProducerRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<List<CustomerManagementProducer>> GetByCustomerAsync(string customerId)
    {
        var filter = Builders<CustomerManagementProducer>.Filter.Eq(x => x.CustomerId, customerId);

        return await _context.CustomerManagementProducer
            .Find(filter)
            .ToListAsync();
    }

    public async Task<CustomerManagementProducer?> GetByIdAsync(string id)
    {
        return await _context.CustomerManagementProducer
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(string customerId, List<CustomerManagementProducer> Producer)
    {
        // cancella solo quelle del cliente
        await _context.CustomerManagementProducer.DeleteManyAsync(x => x.CustomerId == customerId);

        await _context.CustomerManagementProducer.InsertManyAsync(Producer);
    }

    public async Task UpdateAsync(string id, CustomerManagementProducer category)
    {
        await _context.CustomerManagementProducer.ReplaceOneAsync(
            x => x.Id ==id,
            category);
    }

    public async Task DeleteAsync(string id)
    {
        await _context.CustomerManagementProducer.DeleteOneAsync(
            x => x.Id == id);
    }
}
