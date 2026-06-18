using FluentFTP;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Engine.Suppliers;
using HeronIntegration.Shared.Entities;
using Microsoft.Extensions.Hosting;
using System.IO;

public class SupplierStockProcessor : ISupplierStockProcessor
{
    private readonly IEnumerable<ISupplierParser> _parsers;
    private readonly ISupplierStockRepository _repo; 
    private readonly ISupplierRepository _supplierRepo;
    private readonly IHostEnvironment _env;
    private readonly ILogger<SupplierStockProcessor> _logger;

    public SupplierStockProcessor(
        IEnumerable<ISupplierParser> parsers,
        ISupplierStockRepository repo,
        ISupplierRepository supplierRepo,
        IHostEnvironment env,
        ILogger<SupplierStockProcessor> logger)
    {
        _parsers = parsers;
        _repo = repo;
        _supplierRepo = supplierRepo;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Downloads the newest FTP file for one active supplier and stores it under SupplierFiles.
    /// </summary>
    public async Task<string?> DownloadAsync(string supplierCode)
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

            FtpListItem? latestFile = null;

            try
            {
                using var ftp = new FtpClient(supplier.FtpHost, supplier.FtpUser, supplier.FtpPassword);

                ftp.Connect();

                var files = ftp.GetListing();

                // Select the newest supplier file available on FTP.
                latestFile = files
                    .Where(x => x.Type == FtpObjectType.File)
                    .OrderByDescending(x => x.Modified)
                    .FirstOrDefault();

                if (latestFile == null)
                    return null;

                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);

                Directory.CreateDirectory(folder);

                var localPath = Path.Combine(folder, latestFile.Name);

                ftp.DownloadFile(localPath, latestFile.FullName);

                ftp.Disconnect();

                return latestFile.FullName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Download FTP fallito per supplier {SupplierCode}; uso ultimo file locale se presente",
                    supplierCode);

                if (Directory.Exists(folder))
                {
                    var file = new DirectoryInfo(folder)
                    .GetFiles()
                    .OrderByDescending(f => f.LastWriteTime)
                    .FirstOrDefault();
                    if (file == null)
                        return null;

                    return file.FullName;
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore download supplier {SupplierCode}", supplierCode);
        }
        return null;

    }

    /// <summary>
    /// Parses the latest local supplier file and replaces that supplier stock snapshot.
    /// </summary>
    public async Task<bool> ImportAsync(string supplierCode)
    {
        try
        {
            var parser = _parsers.FirstOrDefault(x =>
            x.SupplierCode.Equals(supplierCode, StringComparison.OrdinalIgnoreCase));

            if (parser == null)
                throw new Exception($"Parser non trovato per supplier {supplierCode}");

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore import supplier {SupplierCode}", supplierCode);
        }
        return false;
    }

    /// <summary>
    /// Runs download and import for one supplier.
    /// </summary>
    public async Task RunAsync(string supplierCode)
    {
        await DownloadAsync(supplierCode);
        await ImportAsync(supplierCode);
    }

    /// <summary>
    /// Downloads files for all active suppliers.
    /// </summary>
    public async Task DownloadAllAsync()
    {
        var suppliers = await _supplierRepo.GetActiveAsync();

        foreach (var s in suppliers)
            await DownloadAsync(s.Code);
    }

    /// <summary>
    /// Imports latest local files for all active suppliers.
    /// </summary>
    public async Task ImportAllAsync()
    {
        var suppliers = await _supplierRepo.GetActiveAsync();

        foreach (var s in suppliers)
            await ImportAsync(s.Code);
    }

    /// <summary>
    /// Runs download and import for all active suppliers.
    /// </summary>
    public async Task RunAllAsync()
    {
        var suppliers = await _supplierRepo.GetActiveAsync();

        foreach (var s in suppliers)
            await RunAsync(s.Code);
    }
}
