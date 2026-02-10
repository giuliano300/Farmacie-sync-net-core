using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
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
            var resolved = await _resolvedRepo.GetByBatchAsync(batchId);

            foreach (var product in resolved)
            {
                var dto = new MagentoProductDto
                {
                    Sku = product.Aic,
                    Name = product.Aic,
                    Price = product.Price,
                    Quantity = product.Availability
                };

                try
                {
                    await _exporter.ExportAsync(dto);

                    await _exportRepo.SetSuccessAsync(batchId, product.Aic);
                }
                catch (Exception ex)
                {
                    await _exportRepo.SetErrorAsync(batchId, product.Aic, ex.Message);
                }
            }
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
}
