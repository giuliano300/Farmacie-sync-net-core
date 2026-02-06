using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using System.Globalization;
using System.Net;

namespace HeronIntegration.Engine.Workers;

public class SupplierFileImporterWorker : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly ISupplierStockRepository _repo;
    private readonly ILogger<SupplierFileImporterWorker> _logger;

    public SupplierFileImporterWorker(
        IConfiguration config,
        ISupplierStockRepository repo,
        ILogger<SupplierFileImporterWorker> logger)
    {
        _config = config;
        _repo = repo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ImportAllSuppliers();

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private async Task ImportAllSuppliers()
    {
        var suppliers = _config.GetSection("Suppliers").Get<List<SupplierConfig>>();

        foreach (var supplier in suppliers)
        {
            try
            {
                var localFile = await DownloadFileAsync(supplier);

                var items = ParseSupplierFile(localFile, supplier);

                await _repo.ReplaceSupplierDatasetAsync(supplier.Code, items);

                _logger.LogInformation("Supplier {Code} importato", supplier.Code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore import supplier {Code}", supplier.Code);
            }
        }
    }

    private async Task<string> DownloadFileAsync(SupplierConfig supplier)
    {
        var uri = new Uri($"ftp://{supplier.Host}{supplier.RemoteFile}");

        var request = (FtpWebRequest)WebRequest.Create(uri);
        request.Method = WebRequestMethods.Ftp.DownloadFile;
        request.Credentials = new NetworkCredential(supplier.User, supplier.Password);

        var tempFile = Path.GetTempFileName();

        using var response = (FtpWebResponse)await request.GetResponseAsync();
        using var stream = response.GetResponseStream();
        using var fs = File.Create(tempFile);

        await stream.CopyToAsync(fs);

        return tempFile;
    }

    private List<SupplierStock> ParseSupplierFile(string filePath, SupplierConfig supplier)
    {
        var lines = File.ReadAllLines(filePath);

        var list = new List<SupplierStock>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(';');

            if (parts.Length < 3)
                continue;

            list.Add(new SupplierStock
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                SupplierCode = supplier.Code,
                Aic = parts[0],
                Price = decimal.Parse(parts[1], CultureInfo.InvariantCulture),
                Availability = int.Parse(parts[2]),
                Priority = supplier.Priority,
                ImportedAt = DateTime.UtcNow
            });
        }

        return list;
    }

}
