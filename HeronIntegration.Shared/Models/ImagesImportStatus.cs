using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class ImagesImportStatus
    {
        public bool Running { get; set; }

        public int Processed { get; set; }

        public int Total { get; set; }

        public decimal Percent { get; set; }

        public List<string> Inserted { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
