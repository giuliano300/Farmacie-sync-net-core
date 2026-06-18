namespace HeronIntegration.Engine.Suppliers
{
    public class HeringFtpClient : BaseSupplierFtpClient
    {
        public HeringFtpClient(IConfiguration configuration)
            : base(configuration)
        {
        }

        public override string SupplierCode => "HERING";
    }
}
