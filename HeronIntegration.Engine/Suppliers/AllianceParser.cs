using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public class AllianceParser : SupplierParserBase
    {
        public override string SupplierCode => "ALLIANCE";

        public override IEnumerable<SupplierStock> Parse(string filePath)
        {
            foreach (var line in File.ReadLines(filePath))
            {
                var parts = line.Split(';');

                yield return new SupplierStock
                {
                    SupplierCode = SupplierCode,
                    Aic = parts[0],
                    Availability = ToInt(parts[8]),
                    Price = ToDecimal(parts[3].Replace(",",".")),
                    ImportedAt = DateTime.UtcNow
                };
            }
        }
    }
}
