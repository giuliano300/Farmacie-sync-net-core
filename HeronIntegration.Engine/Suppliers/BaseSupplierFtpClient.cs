using FluentFTP;

namespace HeronIntegration.Engine.Suppliers
{
    /// <summary>
    /// Base FTP client for supplier feeds. Credentials and paths are read from configuration
    /// so operational secrets do not live in source code.
    /// </summary>
    public abstract class BaseSupplierFtpClient : ISupplierFtpClient
    {
        private readonly IConfiguration _configuration;

        protected BaseSupplierFtpClient(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public abstract string SupplierCode { get; }

        protected string Host => GetRequiredValue("Host");
        protected string Username => GetRequiredValue("Username");
        protected string Password => GetRequiredValue("Password");
        protected string RemoteFolder => GetRequiredValue("RemoteFolder");

        /// <summary>
        /// Downloads the first file available in the configured supplier remote folder.
        /// </summary>
        public async Task<string> DownloadAsync(string destinationFolder)
        {
            Directory.CreateDirectory(destinationFolder);

            using var client = new AsyncFtpClient(Host, Username, Password);
            await client.Connect();

            var files = await client.GetListing(RemoteFolder);
            var file = files.First(x => x.Type == FtpObjectType.File);

            var localPath = Path.Combine(destinationFolder, file.Name);

            await client.DownloadFile(
                localPath,
                $"{RemoteFolder}/{file.Name}"
            );

            await client.Disconnect();

            return localPath;
        }

        private string GetRequiredValue(string key)
        {
            var value = _configuration[$"SupplierFtp:{SupplierCode}:{key}"];

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Configuration value 'SupplierFtp:{SupplierCode}:{key}' is required.");
            }

            return value;
        }
    }
}
