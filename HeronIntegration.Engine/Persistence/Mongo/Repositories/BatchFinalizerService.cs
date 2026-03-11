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

        public BatchFinalizerService(
            IRawProductRepository rawRepo,
            IEnrichedProductRepository enrichedRepo,
            IResolvedProductRepository resolvedRepo,
            IBatchReportService reportService,
            IExportRepository exportRepo,
            IBatchRepository batchRepo
            )
        {
            _rawRepo = rawRepo;
            _enrichedRepo = enrichedRepo;
            _resolvedRepo = resolvedRepo;
            _reportService = reportService;
            _exportRepo = exportRepo;
            _batchRepo = batchRepo;
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
        }
    }
}
