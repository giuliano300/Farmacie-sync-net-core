using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Suppliers
{
    public class AllianceFtpClient : BaseSupplierFtpClient
    {
        public override string SupplierCode => "ALLIANCE";

        protected override string Host => "ftp.host";
        protected override string Username => "aliance";
        protected override string Password => "w29j09t&F";
        protected override string RemoteFolder => "/alliance";
    }
}
