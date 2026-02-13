using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public class HeringFtpClient : BaseSupplierFtpClient
    {
        public override string SupplierCode => "HERING";

        protected override string Host => "ftp.host";
        protected override string Username => "hering";
        protected override string Password => "x5_70Yf5k";
        protected override string RemoteFolder => "/hering";
    }
}
