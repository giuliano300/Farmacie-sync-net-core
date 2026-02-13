using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public interface ISupplierParser
    {
        string SupplierCode { get; }

        IEnumerable<SupplierStock> Parse(string filePath);
    }
}
