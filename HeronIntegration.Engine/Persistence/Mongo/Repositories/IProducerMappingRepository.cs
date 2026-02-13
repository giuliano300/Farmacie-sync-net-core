using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IProducerMappingRepository
{
    Task<ProducerMapping?> FindAsync(string customerId, string sourceProducer);
    Task<List<ProducerMapping>?> GetByCustomerAsync(string customerId);
}