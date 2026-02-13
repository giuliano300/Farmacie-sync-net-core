using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Engine.Suppliers;
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

    public async Task DownloadAsync(string supplierCode)
    {
        var ftp = _ftpClients.First(x =>
        x.SupplierCode.Equals(supplierCode, StringComparison.OrdinalIgnoreCase));

        var root = _env.ContentRootPath;
        var parent = Directory.GetParent(root)!.FullName;

        var folder = Path.Combine(
            parent,
            "SupplierFiles",
            supplierCode.ToUpper()
        );
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        await ftp.DownloadAsync(folder);
    }

    public async Task ImportAsync(string supplierCode)
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
