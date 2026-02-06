using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

public class CustomerRepository : ICustomerRepository
{
    private readonly MongoContext _context;

    public CustomerRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task InsertAsync(Customer customer)
    {
        await _context.Customers.InsertOneAsync(customer);
    }

    public async Task<List<Customer>> GetActiveAsync()
    {
        return await _context.Customers
            .Find(x => x.Active)
            .ToListAsync();
    }

    public async Task<List<Customer>> GetAllAsync()
    => await _context.Customers.Find(_ => true).ToListAsync();

    public async Task<Customer?> GetByIdAsync(string id)
        => await _context.Customers.Find(x => x.Id == ObjectId.Parse(id)).FirstOrDefaultAsync();

    public async Task UpdateAsync(Customer customer)
        => await _context.Customers.ReplaceOneAsync(x => x.Id == customer.Id, customer);

    public async Task DeleteAsync(string id)
        => await _context.Customers.DeleteOneAsync(x => x.Id == ObjectId.Parse(id));
}
