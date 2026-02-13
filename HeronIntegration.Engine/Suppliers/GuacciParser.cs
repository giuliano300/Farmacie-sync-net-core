using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public class GuacciParser : SupplierParserBase
    {
        public override string SupplierCode => "GUACCI";

        public override IEnumerable<SupplierStock> Parse(string filePath)
        {
            foreach (var line in File.ReadLines(filePath).Skip(1))
            {
                var parts = line.Split(';');

                yield return new SupplierStock
                {
                    SupplierCode = SupplierCode,
                    Aic = parts[0],
                    Availability = ToInt(parts[3]),
                    Price = ToDecimal(parts[4]),
                    ImportedAt = DateTime.UtcNow
                };
            }
        }
    }
}
