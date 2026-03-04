using HeronIntegration.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class BatchStatusResponse
    {
        public bool CanStartNewBatch { get; set; }

        public string? RunningBatchId { get; set; }

        public string? CurrentStep { get; set; }

        public StepStatus? StepStatus { get; set; }
    }
}
