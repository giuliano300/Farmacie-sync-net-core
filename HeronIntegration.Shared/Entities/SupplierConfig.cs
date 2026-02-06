using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Entities
{
    public class SupplierConfig
    {
        public string Code { get; set; } = default!;
        public string Host { get; set; } = default!;
        public string User { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string RemoteFile { get; set; } = default!;
        public int Priority { get; set; }
    }
}
