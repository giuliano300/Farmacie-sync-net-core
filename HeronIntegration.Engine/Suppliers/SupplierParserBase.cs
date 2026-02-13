using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public abstract class SupplierParserBase : ISupplierParser
    {
        public abstract string SupplierCode { get; }

        protected int ToInt(string v)
            => int.TryParse(v, out var x) ? x : 0;

        protected decimal ToDecimal(string v)
            => decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var x)
                ? x : 0;

        public abstract IEnumerable<SupplierStock> Parse(string filePath);
    }
}
