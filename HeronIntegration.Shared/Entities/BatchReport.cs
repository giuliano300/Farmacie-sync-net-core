using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Entities
{
    public class BatchReport
    {
        public string BatchId { get; set; } = default!;
        public DateTime FinishedAt { get; set; }
        public int TotalProducts { get; set; }
        public int Success { get; set; }
        public int Errors { get; set; }
    }
}
