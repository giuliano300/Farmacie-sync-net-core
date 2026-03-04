using FluentFTP;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Engine.Suppliers;
using HeronIntegration.Shared.Entities;
using Microsoft.Extensions.Hosting;

public class SupplierStockProcessor : ISupplierStockProcessor
{
    private readonly IEnumerable<ISupplierFtpClient> _ftpClients;
    private readonly IEnumerable<ISupplierParser> _parsers;
    private readonly ISupplierStockRepository _repo; 
    private readonly ISupplierRepository _supplierRepo;
    private readonly IHostEnvironment _env;

    private readonly Dictionary<string, string> _files = new();

    public SupplierStockProcessor(
        IEnumerable<ISupplierFtpClient> ftpClients,
        IEnumerable<ISupplierParser> parsers,
        ISupplierStockRepository repo,
        ISupplierRepository supplierRepo,
        IHostEnvironment env)
    {
        _ftpClients = ftpClients;
        _parsers = parsers;
        _repo = repo;
        _supplierRepo = supplierRepo;
        _env = env;
    }

    public async Task<string> DownloadAsync(string supplierCode)
    {
        try
        {
            var supplier = await _supplierRepo.GetByCode(supplierCode);

            if (supplier == null)
                throw new Exception($"Supplier {supplierCode} non trovato");

            var root = _env.ContentRootPath;
            var parent = Directory.GetParent(root)!.FullName;

            var folder = Path.Combine(
                parent,
                "SupplierFiles",
                supplierCode.ToUpper()
            );
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);

            Directory.CreateDirectory(folder);

            var ftp = new FtpClient(supplier.FtpHost, supplier.FtpUser, supplier.FtpPassword);

            ftp.Connect();

            var fileName = Path.GetFileName(supplier.RemoteFile);

            var localPath = Path.Combine(folder, fileName);

            ftp.DownloadFile(localPath, supplier.RemoteFile);

            ftp.Disconnect();

            return fileName;

        }
        catch(Exception e)
        {

        }
        return null;

    }

    public async Task<bool> ImportAsync(string supplierCode)
    {
        try
        {
            var parser = _parsers.First(x =>
            x.SupplierCode.Equals(supplierCode, StringComparison.OrdinalIgnoreCase));

            var root = _env.ContentRootPath;
            var parent = Directory.GetParent(root)!.FullName;

            var folder = Path.Combine(
                parent,
                "SupplierFiles",
                supplierCode.ToUpper()
            );

            if (!Directory.Exists(folder))
                throw new Exception($"Cartella supplier {supplierCode} non esiste");

            var lastFile = new DirectoryInfo(folder)
                .GetFiles()
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (lastFile == null)
                throw new Exception($"Nessun file presente per supplier {supplierCode}");

            var rows = parser.Parse(lastFile.FullName);

            await _repo.ReplaceSupplierAsync(supplierCode, rows);

            return true;
        }
        catch(Exception e)
        {

        }
        return false;
    }

    public async Task RunAsync(string supplierCode)
    {
        await DownloadAsync(supplierCode);
        await ImportAsync(supplierCode);
    }

    public async Task DownloadAllAsync()
    {
        var suppliers = await _supplierRepo.GetActiveAsync();

        foreach (var s in suppliers)
            await DownloadAsync(s.Code);
    }

    public async Task ImportAllAsync()
    {
        var suppliers = await _supplierRepo.GetActiveAsync();

        foreach (var s in suppliers)
            await ImportAsync(s.Code);
    }

    public async Task RunAllAsync()
    {
        var suppliers = await _supplierRepo.GetActiveAsync();

        foreach (var s in suppliers)
            await RunAsync(s.Code);
    }
}
