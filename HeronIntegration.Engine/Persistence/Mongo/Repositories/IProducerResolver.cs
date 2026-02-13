using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public interface IProducerResolver
    {
        Task<string> ResolveAsync(string customerId, string sourceProducer);
        Task<Dictionary<string, string>> LoadMappingsAsync(
         string customerId);
    }
}
