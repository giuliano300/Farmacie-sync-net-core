using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class MagentoInsertResult
    {
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }
}
