using FluentFTP;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using System.Globalization;

namespace HeronIntegration.Engine.Workers;

public class SupplierFileImporterWorker : BackgroundService
{
    // Stock feed imports are periodic snapshots; 30 minutes keeps data fresh without overloading FTP endpoints.
    private static readonly TimeSpan ImportInterval = TimeSpan.FromMinutes(30);

    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SupplierFileImporterWorker> _logger;

    public SupplierFileImporterWorker(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<SupplierFileImporterWorker> logger)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Periodically downloads supplier stock files and refreshes supplier stock collections.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(ImportInterval);

            do
            {
                // Import failures are handled per supplier inside the cycle.
                await ImportAllSuppliers(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Supplier file importer stopped");
        }
    }

    /// <summary>
    /// Imports all configured suppliers, isolating failures per supplier.
    /// </summary>
    private async Task ImportAllSuppliers(CancellationToken token)
    {
        var suppliers = _config.GetSection("Suppliers").Get<List<SupplierConfig>>();
        if (suppliers == null || suppliers.Count == 0)
        {
            _logger.LogDebug("Nessun supplier configurato nella sezione Suppliers.");
            return;
        }

        foreach (var supplier in suppliers)
        {
            token.ThrowIfCancellationRequested();

            // Each supplier owns its temp file; cleanup happens even when parsing/import fails.
            string? localFile = null;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<ISupplierStockRepository>();

                localFile = await DownloadFileAsync(supplier, token);

                var items = ParseSupplierFile(localFile, supplier);

                await repo.ReplaceSupplierAsync(supplier.Code, items);

                _logger.LogInformation(
                    "Supplier {Code} importato: {Count} righe",
                    supplier.Code,
                    items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore import supplier {Code}", supplier.Code);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(localFile) && File.Exists(localFile))
                {
                    File.Delete(localFile);
                }
            }
        }
    }

    /// <summary>
    /// Downloads a supplier file to a temporary path using FluentFTP.
    /// </summary>
    private async Task<string> DownloadFileAsync(SupplierConfig supplier, CancellationToken token)
    {
        ValidateSupplierConfig(supplier);

        // Temp files avoid writing supplier credentials or partial files into the repository workspace.
        var tempFile = Path.GetTempFileName();

        using var client = new AsyncFtpClient(
            supplier.Host,
            supplier.User,
            supplier.Password);

        await client.Connect(token);

        await client.DownloadFile(
            tempFile,
            supplier.RemoteFile,
            FtpLocalExists.Overwrite,
            FtpVerify.None,
            token: token);

        await client.Disconnect(token);

        return tempFile;
    }

    /// <summary>
    /// Parses a semicolon-separated supplier stock file into normalized stock rows.
    /// </summary>
    private List<SupplierStock> ParseSupplierFile(string filePath, SupplierConfig supplier)
    {
        var lines = File.ReadAllLines(filePath);
        var list = new List<SupplierStock>();

        foreach (var line in lines.Skip(1))
        {
            // Supplier files are expected to have a header row and semicolon-separated values.
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(';');

            if (parts.Length < 3)
                continue;

            if (!decimal.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            {
                // Bad rows are skipped instead of failing the whole supplier snapshot.
                _logger.LogWarning("Prezzo non valido per supplier {Code}: {Line}", supplier.Code, line);
                continue;
            }

            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var availability))
            {
                _logger.LogWarning("Disponibilita non valida per supplier {Code}: {Line}", supplier.Code, line);
                continue;
            }

            list.Add(new SupplierStock
            {
                Id = ObjectId.GenerateNewId(),
                SupplierCode = supplier.Code,
                Aic = parts[0],
                Price = price,
                Availability = availability,
                ImportedAt = DateTime.UtcNow
            });
        }

        return list;
    }

    private static void ValidateSupplierConfig(SupplierConfig supplier)
    {
        // Fail fast with precise configuration errors before opening an FTP connection.
        if (string.IsNullOrWhiteSpace(supplier.Code))
            throw new InvalidOperationException("Supplier Code mancante.");

        if (string.IsNullOrWhiteSpace(supplier.Host))
            throw new InvalidOperationException($"Supplier {supplier.Code}: Host mancante.");

        if (string.IsNullOrWhiteSpace(supplier.User))
            throw new InvalidOperationException($"Supplier {supplier.Code}: User mancante.");

        if (string.IsNullOrWhiteSpace(supplier.Password))
            throw new InvalidOperationException($"Supplier {supplier.Code}: Password mancante.");

        if (string.IsNullOrWhiteSpace(supplier.RemoteFile))
            throw new InvalidOperationException($"Supplier {supplier.Code}: RemoteFile mancante.");
    }
}
