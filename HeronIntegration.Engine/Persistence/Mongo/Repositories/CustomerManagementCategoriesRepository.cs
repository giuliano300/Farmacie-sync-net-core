using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

public class CustomerManagementCategoriesRepository : ICustomerManagementCategoriesRepository
{
    private readonly MongoContext _context;

    public CustomerManagementCategoriesRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<List<CustomerManagementCategories>> GetByCustomerAsync(string customerId)
    {
        var filter = Builders<CustomerManagementCategories>.Filter.Eq(x => x.CustomerId, customerId);

        return await _context.CustomerManagementCategories
            .Find(filter)
            .ToListAsync();
    }

    public async Task<CustomerManagementCategories?> GetByIdAsync(string id)
    {
        return await _context.CustomerManagementCategories
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(string customerId, List<CustomerManagementCategories> categories)
    {
        // cancella solo quelle del cliente
        await _context.CustomerManagementCategories.DeleteManyAsync(x => x.CustomerId == customerId);

        await _context.CustomerManagementCategories.InsertManyAsync(categories);
    }

    public async Task UpdateAsync(string id, CustomerManagementCategories category)
    {
        await _context.CustomerManagementCategories.ReplaceOneAsync(
            x => x.Id ==id,
            category);
    }

    public async Task DeleteAsync(string id)
    {
        await _context.CustomerManagementCategories.DeleteOneAsync(
            x => x.Id == id);
    }
}
