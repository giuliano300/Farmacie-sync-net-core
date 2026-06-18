namespace HeronIntegration.Engine.Suppliers
{
    public class SofarmaFtpClient : BaseSupplierFtpClient
    {
        public SofarmaFtpClient(IConfiguration configuration)
            : base(configuration)
        {
        }

        public override string SupplierCode => "SOFARMA";
    }
}
