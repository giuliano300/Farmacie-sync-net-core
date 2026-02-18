using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;

public class MagentoExportStepProcessor : IStepProcessor
{
    public string Step => "Magento";

    private readonly IResolvedProductRepository _resolvedRepo;
    private readonly IExportRepository _exportRepo;
    private readonly IMagentoExporter _exporter;

    public MagentoExportStepProcessor(
        IResolvedProductRepository resolvedRepo,
        IExportRepository exportRepo,
        IMagentoExporter exporter)
    {
        _resolvedRepo = resolvedRepo;
        _exportRepo = exportRepo;
        _exporter = exporter;
    }

    public async Task<StepExecutionResult> ExecuteAsync(string batchId)
    {
        var result = new StepExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            var manufacturersTask = _exporter.GetAttributeOptionsAsync("manufacturer");
            var suppliersTask = _exporter.GetAttributeOptionsAsync("supplier");
            var categoriesTask = _exporter.GetCategoryMapAsync();

            await Task.WhenAll(manufacturersTask, suppliersTask, categoriesTask);

            var manufacturers = manufacturersTask.Result
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

            var suppliers = suppliersTask.Result
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

            var categories = new Dictionary<string, int>(
                categoriesTask.Result,
                StringComparer.OrdinalIgnoreCase
            );

            var resolvedList = await _resolvedRepo.GetByBatchAsync(batchId);

            var semaphore = new SemaphoreSlim(8);

            var tasks = resolvedList.Select(async p =>
            {
                await semaphore.WaitAsync();
                try
                {
                    // Supplier
                    if (!string.IsNullOrWhiteSpace(p.SupplierCode) &&
                        suppliers.TryGetValue(p.SupplierCode.ToLowerInvariant(), out var supplierId))
                        p.SupplierCode = supplierId.ToString();
                    else
                        p.SupplierCode = "0";

                    // Manufacturer
                    if (!string.IsNullOrWhiteSpace(p.Producer) &&
                        manufacturers.TryGetValue(p.Producer.ToLowerInvariant(), out var manufacturerId))
                        p.Producer = manufacturerId.ToString();
                    else
                        p.Producer = "0";

                    // Category
                    var categoryId = ResolveCategoryId(categories, p.SubCategory!.ToLowerInvariant());
                    if (categoryId != null)
                        p.SubCategory = categoryId.ToString();

                    var res = await _exporter.ExportAsync(p);

                    if (res.Success)
                        await _exportRepo.SetSuccessAsync(batchId, p.Aic);
                    else
                        await _exportRepo.SetErrorAsync(batchId, p.Aic, res.ErrorMessage!);
                }
                catch (Exception ex)
                {
                    await _exportRepo.SetErrorAsync(batchId, p.Aic, ex.Message);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        result.FinishedAt = DateTime.UtcNow;
        return result;
    }

    private static int? ResolveCategoryId(
    Dictionary<string, int> categoryMap,
    string categoryName)
    {
        var match = categoryMap
            .FirstOrDefault(x =>
                x.Key.EndsWith("/" + categoryName, StringComparison.OrdinalIgnoreCase)
            );

        if (match.Equals(default(KeyValuePair<string, int>)))
            return null;

        return match.Value;
    }
}
