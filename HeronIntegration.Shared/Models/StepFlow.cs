using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public static class StepFlow
    {
        public static readonly List<string> Steps = new()
        {
            "HeronImport",
            "Farmadati",
            "Suppliers",
            "Magento"
        };

        public static List<string> GetNextSteps(string step)
        {
            var index = StepFlow.Steps.IndexOf(step);

            if (index == -1)
                throw new Exception($"Step non valido: {step}");

            return StepFlow.Steps.Skip(index + 1).ToList();
        }
    }

    public static class StepCollections
    {
        public static readonly Dictionary<string, List<string>> Collections = new()
        {
            { "heronImport", new() { "raw_product", "enriched_product", "resolved_product" } },
            { "farmadati", new() { "enriched_product", "resolved_product" } },
            { "suppliers", new() { "resolved_product" } },
            { "magento", new() { } }
        };
    }
}
