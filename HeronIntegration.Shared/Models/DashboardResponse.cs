using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class DashboardResponse
    {
        public List<BatchDashboardItem> ActiveBatches { get; set; } = new();
        public List<BatchDashboardItem> CompletedBatches { get; set; } = new();
    }
}
