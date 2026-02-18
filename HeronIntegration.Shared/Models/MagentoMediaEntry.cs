using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public class MagentoMediaEntry
    {
        public int id { get; set; }
        public string? media_type { get; set; }
        public string? label { get; set; }
        public int position { get; set; }
        public bool disabled { get; set; }
        public List<string>? types { get; set; }
        public MagentoMediaContent? content { get; set; }
    }

    public class MagentoMediaContent
    {
        public string? base64_encoded_data { get; set; }
        public string? type { get; set; }
        public string? name { get; set; }
    }
}
