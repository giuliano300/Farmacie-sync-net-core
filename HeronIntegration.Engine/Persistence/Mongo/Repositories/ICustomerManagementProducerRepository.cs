using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface ICustomerManagementProducerRepository
{
    Task<List<CustomerManagementProducer>?> GetByCustomerAsync(
        string customerId);

    Task<CustomerManagementProducer?> GetByIdAsync(string id);

    Task CreateAsync(string customerId, List<CustomerManagementProducer> category);

    Task UpdateAsync(string id, CustomerManagementProducer category);

    Task DeleteAsync(string id);
}
