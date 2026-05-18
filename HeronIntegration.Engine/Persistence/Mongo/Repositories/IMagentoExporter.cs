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

    Task ReindexAllAsync(List<ResolvedProduct> products, string batchId, CancellationToken token);

    // 🔹 Attributi e categorie
    Task<Dictionary<string, int>> GetAttributeOptionsAsync(string attributeCode, CancellationToken token);

    Task<Dictionary<string, int>> GetCategoryMapAsync(CancellationToken token);

    Task RunMagentoCronAsync(CancellationToken token);

    Task DisableProductsAsync(List<string> skus, CancellationToken token);

    Task<List<MagentoSlimProduct>> GetMagentoProductsSlimAsync(string batchId, CancellationToken token);

    Task UpdateStockBulkAsync(List<InventoryItem> items, string batchId, CancellationToken token);
    Task UpdateImageBulkAsync(List<ResolvedProduct> items, CancellationToken token);

   Task<MagentoMetadata> GetMagentoMetadataAsync(string batchId, CancellationToken token);

    int? ResolveCategoryId(Dictionary<string, int> categoryMap, string categoryName, CancellationToken token);

    Task ReindexAsync(List<string> skus, string batchId, CancellationToken token);

    Task WaitReindexAsync(string batchId, CancellationToken token);
    Task CleanIndex(CancellationToken token);
    Task CleanCache(CancellationToken token);
    Task DeleteProducts();
    Task<List<CategoryNode>> GetCategoryAsync(CancellationToken token);

    List<CustomerMagentoCategories> FlattenCategoriesNodes(
    List<CategoryNode> nodes,
    string customerId,
    string parentPath = "");

    Task<List<MagentoAttributeOption>> GetAttributeManufacturerAsync(CancellationToken token);

    Task StopMagentoImportAsync(string batchId);


    Task<string?> UploadImageNewAsync(
        ProductImage image,
        string sku,
        string batchId,
        Customer customer,
        CancellationToken token);


    Task<string?> ImportImagesToFtpBulkAsync(
            List<ResolvedProduct> products,
            Customer customer,
            CancellationToken token);

    Task WaitPollingImagesAsync(string batchId, CancellationToken token);
}