using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IMagentoExporter
{
    Task<MagentoInsertResult> ExportAsync(ResolvedProduct product);
    Task ExportBulkAsync(List<ResolvedProduct> product);

    Task<Dictionary<string, int>> GetAttributeOptionsAsync(string attributeCode);

    Task<Dictionary<string, int>> GetCategoryMapAsync();
}
