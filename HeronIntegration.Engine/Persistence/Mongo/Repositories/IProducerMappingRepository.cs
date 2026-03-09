using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IProducerMappingRepository
{
    Task<ProducerMapping?> FindAsync(string customerId, string sourceProducer);
    Task<List<ProducerMapping>?> GetByCustomerAsync(string customerId);

    Task<ProducerMapping?> GetByIdAsync(string id);

    Task CreateAsync(ProducerMapping category);

    Task UpdateAsync(string id, ProducerMapping category);

    Task DeleteAsync(string id);

}