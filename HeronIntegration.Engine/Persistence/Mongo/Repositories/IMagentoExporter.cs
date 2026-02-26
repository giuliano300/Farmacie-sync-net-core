using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IMagentoExporter
{
    // 🔹 Import singolo prodotto
    Task<MagentoInsertResult> ExportAsync(ResolvedProduct product);

    // 🔹 Upload immagini
    Task<MagentoInsertResult> UploadImagesAsync(ResolvedProduct product);

    // 🔹 Import massivo ottimizzato (NUOVO - non rompe nulla)
    Task ImportProductsAsync(IEnumerable<ResolvedProduct> products);

    // 🔹 Attributi e categorie
    Task<Dictionary<string, int>> GetAttributeOptionsAsync(string attributeCode);

    Task<Dictionary<string, int>> GetCategoryMapAsync();

    Task RunMagentoCronAsync();

    Task DisableProductsAsync(List<string> skus);

    Task<List<MagentoSlimProduct>> GetMagentoProductsSlimAsync();

    Task UpdateStockBulkAsync(List<InventoryItem> items);
    Task UpdateImageBulkAsync(List<ResolvedProduct> items);

   Task<MagentoMetadata> GetMagentoMetadataAsync();

    int? ResolveCategoryId(Dictionary<string, int> categoryMap, string categoryName);

}