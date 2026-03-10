using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class CustomerWithBatchStatus
    {
        public Customer Customer { get; set; } = default!;

        public bool CanStartNewBatch { get; set; }

        public string? RunningBatchId { get; set; }
        public string? RunningStepId { get; set; }

        public string? CurrentStep { get; set; }

        public StepStatus? StepStatus { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
