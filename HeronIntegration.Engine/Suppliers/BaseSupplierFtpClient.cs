using FluentFTP; 

namespace HeronIntegration.Engine.Suppliers
{
    public abstract class BaseSupplierFtpClient : ISupplierFtpClient
    {
        public abstract string SupplierCode { get; }

        protected abstract string Host { get; }
        protected abstract string Username { get; }
        protected abstract string Password { get; }
        protected abstract string RemoteFolder { get; }

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
    }
}
