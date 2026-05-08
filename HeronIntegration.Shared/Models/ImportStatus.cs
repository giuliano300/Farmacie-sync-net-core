using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class ImportStatus
    {
        public string? BatchId { get; set; }
        public string? Status { get; set; }
        public int RowsRead { get; set; }
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public string? LastSku { get; set; }


        public ImportStatus()
        {
            BatchId = null;
            Status = null;
            RowsRead = 0;
            Imported = 0;
            Skipped = 0;
            Errors = 0;
            LastSku = null;
        }
    }
}
