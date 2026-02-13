using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public class SofarmaFtpClient : BaseSupplierFtpClient
    {
        public override string SupplierCode => "SOFARMA";

        protected override string Host => "ftp.host";
        protected override string Username => "sofarma";
        protected override string Password => "u5fH5z34_";
        protected override string RemoteFolder => "/sofarma";
    }
}
