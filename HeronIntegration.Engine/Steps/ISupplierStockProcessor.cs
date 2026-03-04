using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Steps
{
    public interface ISupplierStockProcessor
    {
        Task<string> DownloadAsync(string supplierCode);
        Task<bool> ImportAsync(string supplierCode);
        Task RunAsync(string supplierCode);

        Task RunAllAsync();  
        Task DownloadAllAsync();
        Task ImportAllAsync();  
    }
}
