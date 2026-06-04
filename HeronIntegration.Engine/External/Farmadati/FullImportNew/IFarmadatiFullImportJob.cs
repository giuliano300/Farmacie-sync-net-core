using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.External.Farmadati.FullImportNew
{
    public interface IFarmadatiFullImportJob
    {
        Task ExecuteAsync();
    }
}
