using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class MagentoCategory
    {
        public int id { get; set; }
        public int parent_id { get; set; }
        public string name { get; set; }
        public List<MagentoCategory> children_data { get; set; }
    }
}
