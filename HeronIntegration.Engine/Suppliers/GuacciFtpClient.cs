using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public class GuacciFtpClient : BaseSupplierFtpClient
    {
        public override string SupplierCode => "GUACCI";

        protected override string Host => "ftp.host";
        protected override string Username => "guacci";
        protected override string Password => "1Oy9t_8m5";
        protected override string RemoteFolder => "/guacci";
    }
}
