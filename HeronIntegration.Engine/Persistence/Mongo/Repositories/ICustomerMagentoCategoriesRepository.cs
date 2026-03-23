using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface ICustomerMagentoCategoriesRepository
{
    Task<List<CustomerMagentoCategories>?> GetByCustomerAsync(
        string customerId);

    Task<CustomerMagentoCategories?> GetByIdAsync(string id);

    Task CreateAsync(string customerId, List<CustomerMagentoCategories> categories);

    Task UpdateAsync(string id, CustomerMagentoCategories category);

    Task DeleteAsync(string id);
}
