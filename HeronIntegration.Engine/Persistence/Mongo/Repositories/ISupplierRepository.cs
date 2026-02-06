using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public interface ISupplierRepository
    {
        Task InsertAsync(Supplier supplier);
        Task<List<Supplier>> GetActiveAsync();

        Task<List<Supplier>> GetAllAsync();
        Task<Supplier?> GetByIdAsync(string id);
        Task UpdateAsync(Supplier supplier);
        Task DeleteAsync(string id);

    }
}
