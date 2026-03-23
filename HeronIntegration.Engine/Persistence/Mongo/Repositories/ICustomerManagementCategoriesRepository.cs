using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface ICustomerManagementCategoriesRepository
{
    Task<List<CustomerManagementCategories>?> GetByCustomerAsync(
        string customerId);

    Task<CustomerManagementCategories?> GetByIdAsync(string id);

    Task CreateAsync(string customerId, List<CustomerManagementCategories> category);

    Task UpdateAsync(string id, CustomerManagementCategories category);

    Task DeleteAsync(string id);
}
