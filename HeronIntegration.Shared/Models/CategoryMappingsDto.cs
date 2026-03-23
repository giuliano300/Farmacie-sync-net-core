using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class CategoryMappingDto
    {
        public string CustomerId { get; set; }

        public string GestionaleKey { get; set; }

        public int MagentoCategoryId { get; set; }

        public string MagentoPath { get; set; }
    }
}
