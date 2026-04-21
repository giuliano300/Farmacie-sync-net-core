using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public class BatchFinalizerService: IBatchFinalizerService
    {
        private readonly IRawProductRepository _rawRepo;
        private readonly IEnrichedProductRepository _enrichedRepo;
        private readonly IResolvedProductRepository _resolvedRepo;
        private readonly IBatchReportService _reportService;
        private readonly IExportRepository _exportRepo;
        private readonly IBatchRepository _batchRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly IMagentoExporterFactory _magentoExporterFactory;

        public BatchFinalizerService(
            IRawProductRepository rawRepo,
            IEnrichedProductRepository enrichedRepo,
            IResolvedProductRepository resolvedRepo,
            IBatchReportService reportService,
            IExportRepository exportRepo,
            IBatchRepository batchRepo,
            ICustomerRepository customerRepo,
            IMagentoExporterFactory magentoExporterFactory
            )
        {
            _rawRepo = rawRepo;
            _enrichedRepo = enrichedRepo;
            _resolvedRepo = resolvedRepo;
            _reportService = reportService;
            _exportRepo = exportRepo;
            _batchRepo = batchRepo;
            _customerRepo = customerRepo;
            _magentoExporterFactory = magentoExporterFactory;
        }

        public async Task FinalizeBatchAsync(string batchId)
        {
            var report = await _exportRepo.BuildBatchReportAsync(batchId);

            await _batchRepo.CloseAsync(batchId);
            await _exportRepo.DeleteByBatchAsync(batchId);

            await _reportService.SaveBatchReportAsync(report);

            await _rawRepo.DeleteByBatchAsync(batchId);
            await _enrichedRepo.DeleteByBatchAsync(batchId);
            await _resolvedRepo.DeleteByBatchAsync(batchId);


            var batch = await _batchRepo.GetByIdAsync(batchId);
            var customer = await _customerRepo.GetByIdAsync(batch!.CustomerId)
                ?? throw new Exception("Customer non trovato");

            if (customer.Magento == null)
                throw new Exception("Magento config mancante");

            var exporter = _magentoExporterFactory.Create(customer.Magento);

            await exporter.StopMagentoImportAsync(batchId);
        }
    }
}
