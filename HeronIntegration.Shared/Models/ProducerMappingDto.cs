using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class ProducerMappingDto
    {
        public string? Id { get; set; }

        public string CustomerId { get; set; } = null!;

        public string MagentoValue { get; set; } = null!;
        public string MagentoLabel { get; set; } = null!;

        public string ManagementKey { get; set; } = null!;
        public string ManagementLabel { get; set; } = null!;
    }
}
