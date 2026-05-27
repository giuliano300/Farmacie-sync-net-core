using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class MagentoBulkProduct
    {
        public string sku { get; set; }

        public string name { get; set; }

        public string description { get; set; }

        public string short_description { get; set; }

        public decimal price { get; set; }

        public decimal special_price { get; set; }
        public decimal weight { get; set; }

        public int qty { get; set; }
        public int vat { get; set; }
        public string? macroGroup { get; set; }

        public int status { get; set; }

        public int visibility { get; set; }

        public int attribute_set_id { get; set; }

        public string type_id { get; set; }

        public int manufacturer { get; set; }

        public string supplier { get; set; }

        public List<int> website_ids { get; set; }

        public List<int> category_ids { get; set; }
    }
}
