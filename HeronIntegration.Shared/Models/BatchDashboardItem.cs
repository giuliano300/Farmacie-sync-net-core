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
        public ReindexStatus ReindexValues { get; set; } = new();
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
        public int? TotalMagentoProducts { get; set; }
        public int? DownloadedMagentoProducts { get; set; }

        // FLAG CONFIGURAZIONE BATCH
        public bool HasInsertProducts { get; set; }
        public bool HasInsertImages { get; set; }
        public bool HasUpdateQty { get; set; }

        public MagentoStep InsertProducts { get; set; } = new();
        public MagentoStep UpdateProducts { get; set; } = new();
        public MagentoStep InsertImages { get; set; } = new();

        public double ProgressDownload { get; set; }
        public double ProgressTotal { get; set; }
    }

    public class MagentoStep
    {
        public int Total { get; set; }
        public int Processed { get; set; }
        public int Pending { get; set; }
        public int Errors { get; set; }
        public OperationsStatus Status { get; set; }

        public double Progress =>
            Total == 0
                ? 0
                : Math.Round((double)Processed / Total * 100, 2);
    }
}
