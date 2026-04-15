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
        public TypeRun type { get; set; }

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
        public int InsertImages { get; set; }
        public int UpdatePrice { get; set; }
        public int Success { get; set; }
        public int Errors { get; set; }
        public int? totalMagentoProducts { get; set; }
        public int? totalDownloadMagentoProducts { get; set; }

        private double SafeDivide(double a, double b)
        {
            if (b == 0) return 0;
            var result = a / b;
            return double.IsFinite(result) ? result : 0;
        }

        public double ProgressDownload
        {
            get
            {
                if (!totalMagentoProducts.HasValue || !totalDownloadMagentoProducts.HasValue)
                    return 0;

                var total = totalMagentoProducts.Value;
                var downloaded = totalDownloadMagentoProducts.Value;

                if (total == 0 && downloaded == 0)
                    return 100;

                if (total == 0)
                    return 0;

                return Math.Round(SafeDivide(downloaded, total) * 100, 2);
            }
        }

        public double ProgressInsert =>
            Math.Round(SafeDivide(Insert + UpdatePrice + Success, Total) * 100, 2);

        public double ProgressUpdatePrice =>
            Math.Round(SafeDivide(UpdatePrice + Success, totalMagentoProducts ?? 0) * 100, 2);

        public double ProgressInsertImages =>
            Math.Round(SafeDivide(InsertImages, totalMagentoProducts ?? 0) * 100, 2);

        public double Progress =>
            Math.Round(SafeDivide(Success, totalMagentoProducts ?? 0) * 100, 2);
    }
}
