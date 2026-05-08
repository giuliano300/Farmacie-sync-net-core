using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class MagentoImportResponse
    {
        public bool Success { get; set; }

        public int Total { get; set; }

        public List<MagentoImportItem> Items { get; set; }
    }

    public class MagentoImportItem
    {
        public string Sku { get; set; }

        public bool Success { get; set; }

        public int InsertType { get; set; }

        public string Message { get; set; }
    }
}
