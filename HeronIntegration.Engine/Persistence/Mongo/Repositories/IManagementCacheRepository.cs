using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public interface IManagementCacheRepository
    {
        Task<ManagementCache?> GetAsync(string aic);
        Task InsertAsync(ManagementCache cache);

        Task InsertManyAsync(IEnumerable<ManagementCache> caches);

        Task<List<ManagementCache>> GetByAicsAsync(IEnumerable<string> aics);
    }
}
