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
        public StepMetrics Magento { get; set; } = new();
    }

    public class StepMetrics
    {
        public int Total { get; set; }
        public int Success { get; set; }
        public int Errors { get; set; }
        public double Progress =>
        Total == 0 ? 0 : Math.Round((double)Success / Total * 100, 2);
    }
}
