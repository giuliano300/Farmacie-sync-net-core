using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface ICategoryMappingRepository
{
    Task<CategoryMapping?> FindAsync(
        string customerId,
        string sourceCategory,
        string sourceSubCategory);
    Task<List<CategoryMapping>?> GetByCustomerAsync(
        string customerId);
}
