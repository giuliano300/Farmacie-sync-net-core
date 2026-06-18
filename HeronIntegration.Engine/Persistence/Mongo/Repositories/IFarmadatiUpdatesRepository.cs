using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;


public interface IFarmadatiUpdatesRepository
{
    Task<List<FarmadatiUpdates>> FindAsync();
    Task<FarmadatiUpdates?> GetByIdAsync(string id);

    Task CreateAsync(FarmadatiUpdates updates, CancellationToken token);

    Task UpdateAsync(string id, FarmadatiUpdates updates);
    Task UpdateProgressAsync(string id, int total, int count, string status, DateTime? endedAt);

    Task DeleteAsync(string id);
}
