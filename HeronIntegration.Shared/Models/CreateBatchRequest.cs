using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class CreateBatchRequest
    {
        public string CustomerId { get; set; } = default!;
        public string HeronFilePath { get; set; } = default!;
    }
}
