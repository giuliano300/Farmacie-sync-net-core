using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class BatchDashboardItem
    {
        public string BatchId { get; set; } = default!;
        public int SequenceNumber { get; set; }
        public Customer Customer { get; set; } = default!;
        public DateTime StartedAt { get; set; }
        public BatchStatus Status { get; set; }

        public string CurrentStep { get; set; } = default!;
        public StepStatus StepStatus { get; set; }

        public StepMetrics HeronImport { get; set; } = new();
        public StepMetrics Farmadati { get; set; } = new();
        public StepMetrics Suppliers { get; set; } = new();
        public StepMetricsMagento Magento { get; set; } = new();
    }

    public class StepMetrics
    {
        public int Total { get; set; }
        public int Success { get; set; }
        public int Errors { get; set; }
        public double Progress =>
        Total == 0 ? 0 : Math.Round((double)Success / Total * 100, 2);
    }

    public class StepMetricsMagento
    {
        public int Total { get; set; }
        public int Pending { get; set; }
        public int Insert { get; set; }
        public int UpdatePrice { get; set; }
        public int InsertImages { get; set; }
        public int Success { get; set; }
        public int Errors { get; set; }
        public int? totalMagentoProducts { get; set; } = default!;
        public int? totalDownloadMagentoProducts { get; set; } = default!;

        public double ProgressDownload =>
        totalMagentoProducts == null ? 0 : Math.Round((double)(int)totalDownloadMagentoProducts! / (int)totalMagentoProducts! * 100, 2);

        public double ProgressInsert =>
        Total == 0 ? 0 : Math.Round((double)(Insert + UpdatePrice + InsertImages + Success) / Total * 100, 2);
        public double ProgressUpdatePrice =>
        Total == 0 ? 0 : Math.Round((double) (UpdatePrice + InsertImages + Success) / Total * 100, 2);
        public double ProgressInsertImages =>
        Total == 0 ? 0 : Math.Round((double)(InsertImages + Success) / Total * 100, 2);
        public double Progress =>
        Total == 0 ? 0 : Math.Round((double)Success / Total * 100, 2);
    }
}
