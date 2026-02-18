using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IMagentoExporter
{
    Task<MagentoInsertResult> ExportAsync(ResolvedProduct product);
    Task<MagentoInsertResult> UploadImagesAsync(ResolvedProduct product);

    Task<Dictionary<string, int>> GetAttributeOptionsAsync(string attributeCode);

    Task<Dictionary<string, int>> GetCategoryMapAsync();
    Task<string> BuildProductBatchAsync(IEnumerable<ResolvedProduct> products);

    Task TriggerBatchImportAsync(string filePath);

    Task BulkInventoryAsync(IEnumerable<InventoryItem> items);

    void UploadBatchToMagento(string localFile);
}
