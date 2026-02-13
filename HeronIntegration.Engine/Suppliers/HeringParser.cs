using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public class HeringParser : SupplierParserBase
    {
        public override string SupplierCode => "HERING";

        public override IEnumerable<SupplierStock> Parse(string filePath)
        {
            var all = File.ReadAllText(filePath);

            var parts = all.Split('|');

            int recordLength = 11;   // numero campi record (verifica il tuo tracciato)

            for (int i = 0; i + recordLength <= parts.Length; i += recordLength)
            {
                yield return new SupplierStock
                {
                    SupplierCode = SupplierCode,
                    Aic = parts[i + 2],              // colonna AIC reale
                    Availability = ToInt(parts[i + 5]),
                    Price = ToDecimal(parts[i + 8]),
                    ImportedAt = DateTime.UtcNow
                };
            }
        }
    }
}
