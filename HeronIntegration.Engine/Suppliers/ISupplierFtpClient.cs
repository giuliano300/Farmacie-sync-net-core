using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public interface ISupplierFtpClient
    {
        string SupplierCode { get; }

        Task<string> DownloadAsync(string destinationFolder);
    }
}
