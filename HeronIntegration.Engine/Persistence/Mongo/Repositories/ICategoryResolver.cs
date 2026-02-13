using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public interface ICategoryResolver
    {
        Task<(string category, string subCategory)> ResolveAsync(
            string customerId,
            string sourceCategory,
            string sourceSubCategory);
        Task<Dictionary<(string, string), (string, string)>> LoadMappingsAsync(
            string customerId);
    }
}
