using HeronIntegration.Shared.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class ImportSkuStatus
    {
        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("status")]
        public ExportStatus Status { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("time")]
        public DateTime Time { get; set; }

        [JsonProperty("pid")]
        public int Pid { get; set; }
    }
}
