using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public class FarvimaParser : SupplierParserBase
    {
        public override string SupplierCode => "FARVIMA";

        public override IEnumerable<SupplierStock> Parse(string filePath)
        {
            var content = File.ReadAllText(filePath);

            var parts = content.Split('|');

            const int fieldsPerRecord = 11;

            for (int i = 0; i + fieldsPerRecord <= parts.Length; i += fieldsPerRecord)
            {
                var record = parts.Skip(i).Take(fieldsPerRecord).ToArray();

                if (record.Length < fieldsPerRecord)
                    continue;

                yield return new SupplierStock
                {
                    SupplierCode = SupplierCode,
                    Aic = record[2]!.Trim(),
                    Availability = ToInt(record[5]),
                    Price = ToDecimal(record[8]),
                    ImportedAt = DateTime.UtcNow
                };
            }
        }
    }
}
