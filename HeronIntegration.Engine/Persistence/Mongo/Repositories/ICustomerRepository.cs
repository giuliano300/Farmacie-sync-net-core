using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public interface ICustomerRepository
    {
        Task InsertAsync(Customer customer);
        Task<List<Customer>> GetActiveAsync();

        Task<List<Customer>> GetAllAsync();
        Task<Customer?> GetByIdAsync(string id);
        Task UpdateAsync(Customer customer);
        Task DeleteAsync(string id);
    }
}
