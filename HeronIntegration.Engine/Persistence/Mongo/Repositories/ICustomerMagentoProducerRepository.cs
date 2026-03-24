using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface ICustomerMagentoProducerRepository
{
    Task<List<CustomerMagentoProducer>?> GetByCustomerAsync(
        string customerId);

    Task<CustomerMagentoProducer?> GetByIdAsync(string id);

    Task CreateAsync(string customerId, List<CustomerMagentoProducer> categories);

    Task UpdateAsync(string id, CustomerMagentoProducer category);

    Task DeleteAsync(string id);
}
