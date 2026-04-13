using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IProductToExcludeRepository
{
    Task InsertAsync(ProductToExclude product);

    Task InsertManyAsync(IEnumerable<ProductToExclude> products);

    Task<List<ProductToExclude>> GetByCustomerAsync(string customerId);
    Task<ProductToExclude> GetByIdAsync(string Id);

    Task DeleteByCustomerAsync(string customerId);


    Task UpdateAsync(ProductToExclude p);
    Task DeleteAsync(string id);


}
