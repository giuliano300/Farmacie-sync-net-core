using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class InventoryItem
    {
        public string Sku { get; set; } = default!;
        public int Qty { get; set; }
    }
}
