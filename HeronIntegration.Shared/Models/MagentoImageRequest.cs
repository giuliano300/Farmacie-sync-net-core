using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class MagentoImageRequest
    {
        public string sku { get; set; } = default!;

        public List<MagentoImageItem> images { get; set; }
            = new();
    }

    public class MagentoImageItem
    {
        public string name { get; set; } = default!;

        public string base64 { get; set; } = default!;
    }
}
