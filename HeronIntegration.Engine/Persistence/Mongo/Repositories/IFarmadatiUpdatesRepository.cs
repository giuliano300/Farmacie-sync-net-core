using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;


public interface IFarmadatiUpdatesRepository
{
    Task<List<FarmadatiUpdatesWithCustomer>?> FindAsync();
    Task<FarmadatiUpdates?> GetByIdAsync(string id);

    Task CreateAsync(FarmadatiUpdates updates, CancellationToken token);

    Task UpdateAsync(string id, FarmadatiUpdates updates);

    Task DeleteAsync(string id);
}