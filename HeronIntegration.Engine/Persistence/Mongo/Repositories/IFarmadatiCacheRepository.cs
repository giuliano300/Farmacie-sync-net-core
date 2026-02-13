using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public interface IFarmadatiCacheRepository
    {
        Task<FarmadatiCache?> GetAsync(string aic);
        Task InsertAsync(FarmadatiCache cache);

        Task InsertManyAsync(IEnumerable<FarmadatiCache> caches);

        Task<List<FarmadatiCache>> GetByAicsAsync(IEnumerable<string> aics);
    }
}
