using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class MagentoMetadata
    {
        public Dictionary<string, int>? manufacturers { get; set; }
        public Dictionary<string, int>? suppliers { get; set; }
        public Dictionary<string, int>? categories { get; set; }
        public List<MagentoSlimProduct>? magentoProducts { get; set; }
    }
}
