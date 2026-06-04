using HeronIntegration.Shared.Entities;
using MongoDB.Driver;
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
        Task DeleteManyAsync(IEnumerable<FarmadatiCache> caches);
        Task UpdateManyAsync(IEnumerable<FarmadatiCache> updates);

        Task BulkUpsertAsync(IEnumerable<FarmadatiCache> products);

        Task BulkWriteAsync(IEnumerable<WriteModel<FarmadatiCache>> writes);
        Task<List<FarmadatiCache>> GetByAicsAsync(IEnumerable<string> aics);
        Task<List<FarmadatiCache>> GetAll();
    }
}
