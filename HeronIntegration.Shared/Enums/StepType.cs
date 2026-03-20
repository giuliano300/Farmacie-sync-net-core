using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Enums
{
    public enum StepType
    {
        HeronImport = 1,
        Farmadati = 2,
        Suppliers = 3,
        Magento = 4
    }
    public enum TypeRun
    {
        Completo = 0,
        ImpportProdotti = 1,
        UpdatePrezzi = 2,
        ImportImmagini = 3
    }
}
