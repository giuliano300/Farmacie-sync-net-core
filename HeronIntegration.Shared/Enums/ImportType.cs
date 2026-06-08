using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Enums
{
    public enum ImportType
    {
        Full = 1,
        ProductsOnly = 2,
        ProductAndDescription = 3,
        ProductAndMacroCode = 4,
        ProductAndImages = 5
    }
}
