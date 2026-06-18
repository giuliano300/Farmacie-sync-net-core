namespace HeronIntegration.Engine.Suppliers
{
    public class GuacciFtpClient : BaseSupplierFtpClient
    {
        public GuacciFtpClient(IConfiguration configuration)
            : base(configuration)
        {
        }

        public override string SupplierCode => "GUACCI";
    }
}
