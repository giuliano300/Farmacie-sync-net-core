namespace HeronIntegration.Engine.Suppliers
{
    public class AllianceFtpClient : BaseSupplierFtpClient
    {
        public AllianceFtpClient(IConfiguration configuration)
            : base(configuration)
        {
        }

        public override string SupplierCode => "ALLIANCE";
    }
}
