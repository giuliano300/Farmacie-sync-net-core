using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public class SofarmaParser : SupplierParserBase
    {
        public override string SupplierCode => "SOFARMA";

        public override IEnumerable<SupplierStock> Parse(string filePath)
        {
            foreach (var line in File.ReadLines(filePath).Skip(1))
            {
                var parts = line.Split(';');

                yield return new SupplierStock
                {
                    SupplierCode = SupplierCode,
                    Aic = parts[0],
                    Availability = ToInt(parts[5]),
                    Price = ToDecimal(parts[7]),
                    ImportedAt = DateTime.UtcNow
                };
            }
        }
    }
}
