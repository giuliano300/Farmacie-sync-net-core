using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Shared.Models
{
    public class CompleteBatchesItem
    {
        public BatchDashboardItem Batch { get; set; } = new();
        public BatchReport? Report { get; set; } = default!;
    }

}
