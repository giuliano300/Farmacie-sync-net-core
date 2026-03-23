using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface ICategoryMappingRepository
{
    Task<CategoryMapping?> FindAsync(
        string customerId,
        string sourceCategory,
        string sourceSubCategory);
    Task<List<CategoryMapping>?> GetByCustomerAsync(
        string customerId);

    Task<CategoryMapping?> GetByIdAsync(string id);

    Task CreateAsync(CategoryMapping category);

    Task CreateMultipleAsync(string customerId, List<CategoryMappingDto> category);

    Task UpdateAsync(string id, CategoryMapping category);

    Task DeleteAsync(string id);
}
