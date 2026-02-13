using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Driver;

public class CategoryMappingRepository : ICategoryMappingRepository
{
    private readonly MongoContext _context;

    public CategoryMappingRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<CategoryMapping?> FindAsync(
        string customerId,
        string sourceCategory,
        string sourceSubCategory)
    {
        var filter = Builders<CategoryMapping>.Filter.And(
            Builders<CategoryMapping>.Filter.Eq(x => x.CustomerId, customerId),
            Builders<CategoryMapping>.Filter.Eq(x => x.SourceCategory, sourceCategory),
            Builders<CategoryMapping>.Filter.Eq(x => x.SourceSubCategory, sourceSubCategory)
        );

        return await _context.CategoryMappings
            .Find(filter)
            .FirstOrDefaultAsync();
    }

    public async Task<List<CategoryMapping>> GetByCustomerAsync(string customerId)
    {
        var filter = Builders<CategoryMapping>.Filter.Eq(x => x.CustomerId, customerId);

        return await _context.CategoryMappings
            .Find(filter)
            .ToListAsync();
    }
}
