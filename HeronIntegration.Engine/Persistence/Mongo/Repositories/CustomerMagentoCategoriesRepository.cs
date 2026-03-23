using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

public class CustomerMagentoCategoriesRepository : ICustomerMagentoCategoriesRepository
{
    private readonly MongoContext _context;

    public CustomerMagentoCategoriesRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<List<CustomerMagentoCategories>> GetByCustomerAsync(string customerId)
    {
        var filter = Builders<CustomerMagentoCategories>.Filter.Eq(x => x.CustomerId, customerId);

        return await _context.CustomerMagentoCategories
            .Find(filter)
            .ToListAsync();
    }

    public async Task<CustomerMagentoCategories?> GetByIdAsync(string id)
    {
        return await _context.CustomerMagentoCategories
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(string customerId, List<CustomerMagentoCategories> categories)
    {
        // cancella solo quelle del cliente
        await _context.CustomerMagentoCategories.DeleteManyAsync(x => x.CustomerId == customerId);

        await _context.CustomerMagentoCategories.InsertManyAsync(categories);
    }

    public async Task UpdateAsync(string id, CustomerMagentoCategories category)
    {
        await _context.CustomerMagentoCategories.ReplaceOneAsync(
            x => x.Id ==id,
            category);
    }

    public async Task DeleteAsync(string id)
    {
        await _context.CustomerMagentoCategories.DeleteOneAsync(
            x => x.Id == id);
    }

}
