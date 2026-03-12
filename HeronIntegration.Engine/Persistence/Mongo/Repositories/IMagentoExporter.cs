using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IMagentoExporter
{
    // 🔹 Import singolo prodotto
    Task<MagentoInsertResult> ExportAsync(ResolvedProduct product, CancellationToken token);

    // 🔹 Upload immagini
    Task<MagentoInsertResult> UploadImagesAsync(ResolvedProduct product, CancellationToken token);

    // 🔹 Import massivo ottimizzato (NUOVO - non rompe nulla)
    Task ImportProductsAsync(IEnumerable<ResolvedProduct> products, CancellationToken token);

    // 🔹 Attributi e categorie
    Task<Dictionary<string, int>> GetAttributeOptionsAsync(string attributeCode, CancellationToken token);

    Task<Dictionary<string, int>> GetCategoryMapAsync(CancellationToken token);

    Task RunMagentoCronAsync(CancellationToken token);

    Task DisableProductsAsync(List<string> skus, CancellationToken token);

    Task<List<MagentoSlimProduct>> GetMagentoProductsSlimAsync(CancellationToken token);

    Task UpdateStockBulkAsync(List<InventoryItem> items, CancellationToken token);
    Task UpdateImageBulkAsync(List<ResolvedProduct> items, CancellationToken token);

   Task<MagentoMetadata> GetMagentoMetadataAsync(CancellationToken token);

    int? ResolveCategoryId(Dictionary<string, int> categoryMap, string categoryName, CancellationToken token);

}