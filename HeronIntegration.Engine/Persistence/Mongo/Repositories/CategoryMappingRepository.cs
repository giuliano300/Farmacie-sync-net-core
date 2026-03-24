using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using MongoDB.Bson;
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

    public async Task<CategoryMapping?> GetByIdAsync(string id)
    {
        return await _context.CategoryMappings
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(CategoryMapping category)
    {
        await _context.CategoryMappings.InsertOneAsync(category);
    }

    public async Task CreateMultipleAsync(string customerId, List<CategoryMappingDto> category)
    {
        var mappings = category.Select(x =>
        {
            var split = x.GestionaleKey.Split('|');

            return new CategoryMapping
            {
                Id = $"{x.CustomerId}_{x.GestionaleKey}_{x.MagentoCategoryId}",

                CustomerId = x.CustomerId,

                SourceCategory = split[0],
                SourceSubCategory = split.Length > 1 ? split[1] : "",

                GestionaleKey = x.GestionaleKey,

                MagentoCategoryId = x.MagentoCategoryId,
                MagentoPath = x.MagentoPath
            };
        }).ToList();

        await _context.CategoryMappings.InsertManyAsync(mappings);

    }

    public async Task UpdateAsync(string id, CategoryMapping category)
    {
        await _context.CategoryMappings.ReplaceOneAsync(
            x => x.Id ==id,
            category);
    }

    public async Task DeleteAsync(string id)
    {
        await _context.CategoryMappings.DeleteOneAsync(
            x => x.Id == id);
    }
}
