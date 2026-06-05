using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using ServiceReference1;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Serilog.Context;

namespace HeronIntegration.Engine.External.Farmadati.FullImportNew
{
    public class FarmadatiFullImportJob : IFarmadatiFullImportJob
    {
        private readonly IFarmadatiCacheRepository _cache;
        private readonly ImageStorageService _imageStorage;
        private readonly FarmadatiImageDownloader _imageDownloader;
        private readonly FarmadatiItaliaWebServicesM2Client _client;
        private readonly IFarmadatiUpdatesRepository _updatesRepository;
        private readonly ILogger<FarmadatiFullImportJob> _logger;

        private readonly string _username;
        private readonly string _password;
        private readonly string _rootPath;

        public FarmadatiFullImportJob(
        IFarmadatiCacheRepository cache,
        ImageStorageService imageStorage,
        FarmadatiImageDownloader imageDownloader,
        IFarmadatiUpdatesRepository updatesRepository,
        IConfiguration configuration,
        ILogger<FarmadatiFullImportJob> logger)
        {
            _cache = cache;

            _imageStorage = imageStorage;
            _imageDownloader = imageDownloader;
            _logger = logger;
            _updatesRepository = updatesRepository;

            _client = new FarmadatiItaliaWebServicesM2Client(
                FarmadatiItaliaWebServicesM2Client.EndpointConfiguration
                    .BasicHttpBinding_FarmadatiItaliaWebServicesM2);

            _username = configuration["Farmadati:Username"]!;
            _password = configuration["Farmadati:Password"]!;
            _rootPath = configuration["Farmadati:RootPath"]!;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            using (LogContext.PushProperty("ImportType", "Farmadati"))
            {
                var swInit = Stopwatch.StartNew();
                cancellationToken.ThrowIfCancellationRequested();

                //CREA UNA NUOVA OPERAZIONE DI IMPORT, IN MODO DA AVERE UN LOG DELL'IMPORTAZIONE E POTERLA MONITORARE MEGLIO
                var fupd = new FarmadatiUpdates
                {
                    StartedAt = DateTime.UtcNow,
                    Status = "Downloading",
                    productNumber = 0,
                    productWorked = 0
                };

                await _updatesRepository.CreateAsync(
                    fupd,
                    CancellationToken.None);

                var updateId = fupd.Id;

                _logger.LogInformation("Farmadati import avviato. UpdateId: {UpdateId}", updateId);

                int totalProducts = 0;
                int worked = 0;

                try
                {
                    // DOWNLOAD
                    cancellationToken.ThrowIfCancellationRequested();

                    var rootFolder = Path.Combine(
                        AppContext.BaseDirectory,
                        _rootPath);

                    if (Directory.Exists(rootFolder))
                        Directory.Delete(rootFolder, true);

                    _logger.LogInformation("Download dataset TE001 iniziato");
                    cancellationToken.ThrowIfCancellationRequested();
                    var te001Folder = await DownloadDatasetAsync("TE001");
                    _logger.LogInformation("Download dataset TE001 completato. Folder: {Folder}", te001Folder);

                    _logger.LogInformation("Download dataset TE002 iniziato");
                    cancellationToken.ThrowIfCancellationRequested();
                    var te002Folder = await DownloadDatasetAsync("TE002");
                    _logger.LogInformation("Download dataset TE002 completato. Folder: {Folder}", te002Folder);

                    _logger.LogInformation("Download dataset TE006 iniziato");
                    cancellationToken.ThrowIfCancellationRequested();
                    var te006Folder = await DownloadDatasetAsync("TE006");
                    _logger.LogInformation("Download dataset TE006 completato. Folder: {Folder}", te006Folder);

                    _logger.LogInformation("Download dataset TE011 iniziato");
                    cancellationToken.ThrowIfCancellationRequested();
                    var te011Folder = await DownloadDatasetAsync("TE011");
                    _logger.LogInformation("Download dataset TE011 completato. Folder: {Folder}", te011Folder);

                    _logger.LogInformation("Download dataset TE015 iniziato");
                    cancellationToken.ThrowIfCancellationRequested();
                    var te015Folder = await DownloadDatasetAsync("TE015");
                    _logger.LogInformation("Download dataset TE015 completato. Folder: {Folder}", te015Folder);

                    _logger.LogInformation("Download dataset TE008 iniziato");
                    cancellationToken.ThrowIfCancellationRequested();
                    var te008Folder = await DownloadDatasetAsync("TE008");
                    _logger.LogInformation("Download dataset TE008 completato. Folder: {Folder}", te008Folder);

                    _logger.LogInformation("Download dataset TR039 iniziato");
                    cancellationToken.ThrowIfCancellationRequested();
                    var tr039Folder = await DownloadDatasetAsync("TR039");
                    _logger.LogInformation("Download dataset TR039 completato. Folder: {Folder}", tr039Folder);

                    _logger.LogInformation("Download dataset TR036 iniziato");
                    cancellationToken.ThrowIfCancellationRequested();
                    var tr036Folder = await DownloadDatasetAsync("TR036");
                    _logger.LogInformation("Download dataset TR036 completato. Folder: {Folder}", tr036Folder);

                    _logger.LogInformation("Download dataset TE004 iniziato");
                    cancellationToken.ThrowIfCancellationRequested();
                    var te004Folder = await DownloadDatasetAsync("TE004");
                    _logger.LogInformation("Download dataset TE004 completato. Folder: {Folder}", te004Folder);

                    _logger.LogInformation("Download dataset TE009 iniziato");
                    cancellationToken.ThrowIfCancellationRequested();
                    var te009Folder = await DownloadDatasetAsync("TE009");
                    _logger.LogInformation("Download dataset TE009 completato. Folder: {Folder}", te009Folder);

                    cancellationToken.ThrowIfCancellationRequested();

                    var te001Xml = Directory.GetFiles(te001Folder, "*.xml", SearchOption.AllDirectories).First();
                    var te002Xml = Directory.GetFiles(te002Folder, "*.xml", SearchOption.AllDirectories).First();
                    var te006Xml = Directory.GetFiles(te006Folder, "*.xml", SearchOption.AllDirectories).First();
                    var te011Xml = Directory.GetFiles(te011Folder, "*.xml", SearchOption.AllDirectories).First();
                    var te015Xml = Directory.GetFiles(te015Folder, "*.xml", SearchOption.AllDirectories).First();

                    var te008Xml = Directory.GetFiles(te008Folder, "*.xml", SearchOption.AllDirectories).First();
                    var tr039Xml = Directory.GetFiles(tr039Folder, "*.xml", SearchOption.AllDirectories).First();
                    var tr036Xml = Directory.GetFiles(tr036Folder, "*.xml", SearchOption.AllDirectories).First();

                    var te004Xml = Directory.GetFiles(te004Folder, "*.xml", SearchOption.AllDirectories).First();
                    var te009Xml = Directory.GetFiles(te009Folder, "*.xml", SearchOption.AllDirectories).First();


                    // IMPORT BASE
                    var products = new Dictionary<string, FarmadatiCache>();

                    // Carico prima TE001 per avere il numero totale di prodotti da importare, in modo da poter aggiornare correttamente il progresso dell'importazione
                    var sw = Stopwatch.StartNew();
                    LoadTe001(products, te001Xml, cancellationToken);
                    sw.Stop();
                    _logger.LogInformation("LoadTE001 completato in {Seconds} secondi", sw.Elapsed.TotalSeconds);

                    // Carico prima TE002 per avere il numero totale di prodotti da importare, in modo da poter aggiornare correttamente il progresso dell'importazione
                    sw = Stopwatch.StartNew();
                    LoadTe002(products, te002Xml, cancellationToken);
                    sw.Stop();
                    _logger.LogInformation("LoadTE002 completato in {Seconds} secondi", sw.Elapsed.TotalSeconds);

                    // Carico prima TE006 per avere il numero totale di prodotti da importare, in modo da poter aggiornare correttamente il progresso dell'importazione
                    sw = Stopwatch.StartNew();
                    LoadTe006(products, te006Xml, cancellationToken);
                    sw.Stop();
                    _logger.LogInformation("LoadTE006 completato in {Seconds} secondi", sw.Elapsed.TotalSeconds);

                    // Carico prima TE011 per avere il numero totale di prodotti da importare, in modo da poter aggiornare correttamente il progresso dell'importazione
                    sw = Stopwatch.StartNew();
                    LoadTe011(products, te011Xml, cancellationToken);
                    sw.Stop();
                    _logger.LogInformation("LoadTE011 completato in {Seconds} secondi", sw.Elapsed.TotalSeconds);

                    // Carico prima TE015 per avere il numero totale di prodotti da importare, in modo da poter aggiornare correttamente il progresso dell'importazione
                    sw = Stopwatch.StartNew();
                    LoadTe015(products, te015Xml, cancellationToken);
                    sw.Stop();
                    _logger.LogInformation("LoadTE015 completato in {Seconds} secondi", sw.Elapsed.TotalSeconds);

                    totalProducts = products.Count;

                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        worked,
                        "Inizio import", null);

                    foreach (var batch in products.Values.Chunk(5000))
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            await _cache.BulkUpsertAsync(batch);

                            worked += batch.Count();

                            await _updatesRepository.UpdateProgressAsync(
                                updateId!,
                                totalProducts,
                                worked,
                                "Import in corso", null);

                            _logger.LogInformation(
                                "Import prodotti: {Worked}/{Total}",
                                worked,
                                totalProducts);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Errore durante l'importazione batch. UpdateId: {UpdateId}", updateId);
                            throw;
                        }
                    }

                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        totalProducts,
                        "Import completato", null);

                    products.Clear();


                    //INSERIMENTO DESCRIZIONE LUNGA
                    sw = Stopwatch.StartNew();
                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        totalProducts,
                        "Inserimento descrizione lunga", null);
                    cancellationToken.ThrowIfCancellationRequested();
                    await MergeTe008Async(te008Xml, cancellationToken);
                    sw.Stop();
                    _logger.LogInformation("Inserimento descrizione lunga completato in {Seconds} secondi", sw.Elapsed.TotalSeconds);

                    //INSERIMENTO DESCRIZIONE BREVE
                    sw = Stopwatch.StartNew();
                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        totalProducts,
                        "Inserimento descrizione breve", null);
                    cancellationToken.ThrowIfCancellationRequested();
                    await MergeTr039Async(tr039Xml, cancellationToken);
                    sw.Stop();
                    _logger.LogInformation("Inserimento descrizione breve completato in {Seconds} secondi", sw.Elapsed.TotalSeconds);

                    //INSERIMENTO MACRO GRUOP CODE
                    sw = Stopwatch.StartNew();
                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        totalProducts,
                        "Inserimento codice macro group", null);
                    cancellationToken.ThrowIfCancellationRequested();
                    await MergeTr036Async(tr036Xml, cancellationToken);
                    sw.Stop();
                    _logger.LogInformation("Inserimento codice macro group completato in {Seconds} secondi", sw.Elapsed.TotalSeconds);


                    //INSERIMENTO IMMAGINI
                    //IMMAGINI ESISTENTI
                    sw = Stopwatch.StartNew();
                    var existingFiles = await _imageStorage.GetAllFilesAsync();
                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        totalProducts,
                        "Inserimento immagini", null);
                    cancellationToken.ThrowIfCancellationRequested();
                    await MergeTe004Async(te004Xml, existingFiles, cancellationToken);
                    sw.Stop();
                    _logger.LogInformation("Inserimento immagini Te004 completato in {Seconds} secondi", sw.Elapsed.TotalSeconds);

                    sw = Stopwatch.StartNew();
                    cancellationToken.ThrowIfCancellationRequested();
                    await MergeTe009Async(te009Xml, existingFiles, cancellationToken);
                    sw.Stop();
                    _logger.LogInformation("Inserimento immagini Te009 completato in {Seconds} secondi", sw.Elapsed.TotalSeconds);


                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        totalProducts,
                        "Completed", DateTime.Now);

                    swInit.Stop();

                    _logger.LogInformation("Importazione completata in {Seconds} secondi. UpdateId: {UpdateId}", swInit.Elapsed.TotalSeconds, updateId);
                }
                catch (OperationCanceledException)
                {
                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        worked,
                        "Cancelled",
                        DateTime.Now);

                    swInit.Stop();

                    _logger.LogInformation("Importazione cancellata. UpdateId: {UpdateId}, Durata {Seconds} secondi", updateId, swInit.Elapsed.TotalSeconds);

                    throw;
                }
                catch (Exception ex)
                {
                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        worked,
                        "Error", DateTime.Now);

                    swInit.Stop();
                    _logger.LogError(ex, "Errore durante l'importazione. UpdateId: {UpdateId}, Durata {Seconds} secondi", updateId, swInit.Elapsed.TotalSeconds);

                    throw;
                }
            }
        }

        private async Task MergeTe004Async(
            string xmlPath,
            Dictionary<string, ObjectId> existingFiles, CancellationToken cancellationToken)
        {
            var updates =
                new List<WriteModel<FarmadatiCache>>();

            foreach (var record in ReadRecords(xmlPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!record.TryGetValue("FDI_T456", out var aic))
                        continue;

                    if (!record.TryGetValue("FDI_T459", out var fileName))
                        continue;

                    ObjectId fileId;

                    if (existingFiles.TryGetValue(
                            fileName,
                            out var existingId))
                    {
                        fileId = existingId;
                    }
                    else
                    {
                        var image =
                            await _imageDownloader.DownloadAsync(
                                "TE004",
                                fileName);

                        if (image == null)
                            continue;

                        fileId =
                            await _imageStorage.SaveAsync(
                                fileName,
                                image.Value.Bytes,
                                image.Value.MimeType);

                        existingFiles[fileName] = fileId;
                    }

                    var productImage =
                        new ProductImage
                        {
                            GridFsId = fileId,
                            AltText = fileName,
                            Type = "Farmadati"
                        };

                    var filter = Builders<FarmadatiCache>.Filter.And(
                        Builders<FarmadatiCache>.Filter.Eq(
                            x => x.Aic,
                            aic),
                        Builders<FarmadatiCache>.Filter.Not(
                            Builders<FarmadatiCache>.Filter.ElemMatch(
                                x => x.Images,
                                i => i.GridFsId == fileId)));

                    var update = Builders<FarmadatiCache>.Update
                        .AddToSet(
                            x => x.Images,
                            productImage);

                    updates.Add(
                        new UpdateOneModel<FarmadatiCache>(
                            filter,
                            update));

                    if (updates.Count >= 100)
                    {
                        await _cache.BulkWriteAsync(
                            updates);

                        updates.Clear();
                    }
                }
                catch (Exception ex)
                {
                    // Log dell'errore, ma continua con il prossimo record
                    //Console.WriteLine($"Errore durante l'elaborazione del record: {ex.Message}");
                }
            }

            if (updates.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    await _cache.BulkWriteAsync(
                        updates);
                }
                catch (Exception ex)
                {
                    // Log dell'errore
                    //Console.WriteLine($"Errore durante il BulkWriteAsync: {ex.Message}"); 
                }
            }
        }

        private async Task MergeTe009Async(
            string xmlPath,
            Dictionary<string, ObjectId> existingFiles, CancellationToken cancellationToken)
        {
            var updates =
                new List<WriteModel<FarmadatiCache>>();

            foreach (var record in ReadRecords(xmlPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try 
                { 
                    if (!record.TryGetValue("FDI_0840", out var aic))
                        continue;

                    if (!record.TryGetValue("FDI_0843", out var fileName))
                        continue;

                    ObjectId fileId;

                    if (existingFiles.TryGetValue(
                            fileName,
                            out var existingId))
                    {
                        fileId = existingId;
                    }
                    else
                    {
                        var image =
                            await _imageDownloader.DownloadAsync(
                                "TE009",
                                fileName);

                        if (image == null)
                            continue;

                        fileId =
                            await _imageStorage.SaveAsync(
                                fileName,
                                image.Value.Bytes,
                                image.Value.MimeType);

                        existingFiles[fileName] = fileId;
                    }

                    var productImage =
                        new ProductImage
                        {
                            GridFsId = fileId,
                            AltText = fileName,
                            Type = "Farmadati"
                        };

                    var filter = Builders<FarmadatiCache>.Filter.And(
                        Builders<FarmadatiCache>.Filter.Eq(
                            x => x.Aic,
                            aic),
                        Builders<FarmadatiCache>.Filter.Not(
                            Builders<FarmadatiCache>.Filter.ElemMatch(
                                x => x.Images,
                                i => i.GridFsId == fileId)));

                    var update = Builders<FarmadatiCache>.Update
                        .AddToSet(
                            x => x.Images,
                            productImage);

                    updates.Add(
                        new UpdateOneModel<FarmadatiCache>(
                            filter,
                            update));

                    if (updates.Count >= 100)
                    {
                        await _cache.BulkWriteAsync(
                            updates);

                        updates.Clear();
                    }
                }
                catch (Exception ex)
                {
                    // Log dell'errore, ma continua con il prossimo record
                    //Console.WriteLine($"Errore durante l'elaborazione del record: {ex.Message}");
                }
            }

            if (updates.Count > 0)
            {
                try 
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    await _cache.BulkWriteAsync(
                        updates);
                }
                catch (Exception ex)
                {
                    // Log dell'errore
                    //Console.WriteLine($"Errore durante il BulkWriteAsync: {ex.Message}"); 
                }
            }
        }

        private async Task MergeTe008Async(string xmlPath, CancellationToken cancellationToken)
        {
            var updates = new List<WriteModel<FarmadatiCache>>();

            foreach (var record in ReadRecords(xmlPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try 
                { 

                    if (!record.TryGetValue("FDI_0001", out var aic))
                        continue;

                    if (!record.TryGetValue("FDI_1702", out var description))
                        continue;

                    updates.Add(
                        new UpdateOneModel<FarmadatiCache>(
                            Builders<FarmadatiCache>.Filter.Eq(
                                x => x.Aic,
                                aic),
                            Builders<FarmadatiCache>.Update
                                .Set(
                                    x => x.LongDescription,
                                    description)
                                .Set(
                                    x => x.UpdatedAt,
                                    DateTime.UtcNow)));

                    if (updates.Count >= 1000)
                    {
                        _logger.LogInformation(
                            "Merge Te008 prodotti: {Count}", updates.Count);

                        await _cache.BulkWriteAsync(updates);
                        updates.Clear();

                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore MergeTe008");
                }
            }

            if (updates.Count > 0)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    _logger.LogInformation(
                        "Merge Te008 prodotti: {Count}", updates.Count);

                    await _cache.BulkWriteAsync(updates);

                    updates.Clear();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore MergeTe008");
                }
            }
        }

        private async Task MergeTr039Async(string xmlPath, CancellationToken cancellationToken)
        {
            var updates = new List<WriteModel<FarmadatiCache>>();

            foreach (var record in ReadRecords(xmlPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try 
                { 
                    if (!record.TryGetValue("FDI_0001", out var aic))
                        continue;

                    if (!record.TryGetValue("FDI_4875", out var description))
                        continue;

                    updates.Add(
                        new UpdateOneModel<FarmadatiCache>(
                            Builders<FarmadatiCache>.Filter.Eq(
                                x => x.Aic,
                                aic),
                            Builders<FarmadatiCache>.Update
                                .Set(
                                    x => x.ShortDescription,
                                    description)
                                .Set(
                                    x => x.UpdatedAt,
                                    DateTime.UtcNow)));

                    if (updates.Count >= 1000)
                    {
                        _logger.LogInformation("Merge Tr039 prodotti: {Count}", updates.Count);
                        
                        await _cache.BulkWriteAsync(updates);
                        updates.Clear();
                    }
                }
                catch (Exception ex)
                {
                   _logger.LogError(ex, "Errore MergeTr039");
                }
            }

            if (updates.Count > 0)
            {
                try 
                {
                    _logger.LogInformation("Merge Tr039 prodotti: {Count}", updates.Count);

                    cancellationToken.ThrowIfCancellationRequested();
                    await _cache.BulkWriteAsync(updates);

                    updates.Clear();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore MergeTr039");
                }
            }
        }

        private async Task MergeTr036Async(string xmlPath, CancellationToken cancellationToken)
        {
            var updates = new List<WriteModel<FarmadatiCache>>();

            foreach (var record in ReadRecords(xmlPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (!record.TryGetValue("FDI_T045", out var aic))
                        continue;

                    if (!record.TryGetValue("FDI_T043", out var macroGroupCode))
                        continue;

                    updates.Add(
                        new UpdateOneModel<FarmadatiCache>(
                            Builders<FarmadatiCache>.Filter.Eq(
                                x => x.Aic,
                                aic),
                            Builders<FarmadatiCache>.Update
                                .Set(
                                    x => x.MacroGroupCode,
                                    macroGroupCode)
                                .Set(
                                    x => x.UpdatedAt,
                                    DateTime.UtcNow)));

                    if (updates.Count >= 1000)
                    {
                        _logger.LogInformation("Merge Tr036 prodotti: {Count}", updates.Count);
                        await _cache.BulkWriteAsync(updates);
                        updates.Clear();
                    }
                }
                catch (Exception ex) 
                {
                    _logger.LogError(ex, "Errore MergeTr036");
                }
            }

            if (updates.Count > 0)
            {
                try
                {
                    _logger.LogInformation("Merge Tr036 prodotti: {Count}", updates.Count);

                    cancellationToken.ThrowIfCancellationRequested();
                    await _cache.BulkWriteAsync(updates);
                    updates.Clear();
                }
                catch (Exception ex)
                {
                   _logger.LogError(ex, "Errore MergeTr036");
                }
            }
        }

        private async Task<string> DownloadDatasetAsync(string datasetCode)
        {
            try
            {
                _logger.LogInformation("Richiesta download dataset {DatasetCode} in corso...", datasetCode);
                var result = await _client.GetDataSetAsync(
                   _username,
                   _password,
                   datasetCode,
                   "GETRECORDS",
                   1);

                if (result.CodEsito != "OK")
                    throw new Exception(result.DescEsito);

                var rootFolder = Path.Combine(
                    AppContext.BaseDirectory,
                    _rootPath);

                if (!Directory.Exists(rootFolder))
                    Directory.CreateDirectory(rootFolder);

                var zipPath = Path.Combine(
                    rootFolder,
                    $"{datasetCode}.zip");

                await File.WriteAllBytesAsync(
                    zipPath,
                    result.ByteListFile);

                var extractFolder = Path.Combine(
                    rootFolder,
                    datasetCode);

                if (Directory.Exists(extractFolder))
                    Directory.Delete(extractFolder, true);

                ZipFile.ExtractToDirectory(
                    zipPath,
                    extractFolder);

                return extractFolder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il download del dataset {DatasetCode}", datasetCode);
                throw;
            }   

        }

        private IEnumerable<Dictionary<string, string>> ReadRecords(string xmlPath)
        {
            var doc = XDocument.Load(xmlPath);

            foreach (var record in doc.Descendants("RECORD"))
            {
                yield return record.Elements()
                    .ToDictionary(
                        e => e.Name.LocalName,
                        e => e.Value);
            }
        }

        private void LoadTe001(Dictionary<string, FarmadatiCache> products, string xmlPath, CancellationToken cancellation)
        {
            foreach (var record in ReadRecords(xmlPath))
            {
                cancellation.ThrowIfCancellationRequested();

                try
                {
                    if (!record.TryGetValue("FDI_0001", out var code))
                        continue;

                    products[code] = new FarmadatiCache
                    {
                        Aic = code,
                        Name = record.GetValueOrDefault("FDI_0004") ?? "",
                        MacroGroup = "P",
                        CachedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        DatasetDate = DateTime.UtcNow
                    };

                    _logger.LogInformation("Loaded product TE001: {Aic} - {Name}", code, record.GetValueOrDefault("FDI_0004") ?? "");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante il caricamento del record TE001 con AIC {Aic}", record.GetValueOrDefault("FDI_0001") ?? "N/A");
                }

            }
        }
        private void LoadTe002(Dictionary<string, FarmadatiCache> products, string xmlPath, CancellationToken cancellation)
        {
            foreach (var record in ReadRecords(xmlPath))
            {
                cancellation.ThrowIfCancellationRequested();

                try
                {
                    if (!record.TryGetValue("FDI_0001", out var aic))
                        continue;

                    var product = new FarmadatiCache
                    {
                        Aic = aic,
                        Name = record.GetValueOrDefault("FDI_0004") ?? string.Empty,

                        // Farmaco
                        MacroGroup = record.GetValueOrDefault("FDI_0008") ?? string.Empty,

                        // Per ora descrizione breve uguale al nome
                        ShortDescription = record.GetValueOrDefault("FDI_0004") ?? string.Empty,

                        CachedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        DatasetDate = DateTime.UtcNow
                    };

                    products[aic] = product;

                    _logger.LogInformation("Loaded product TE002: {Aic} - {Name}", aic, record.GetValueOrDefault("FDI_0004") ?? "");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante il caricamento del record TE002 con AIC {Aic}", record.GetValueOrDefault("FDI_0001") ?? "N/A");
                    throw;
                }
            }
        }
        private void LoadTe006(Dictionary<string, FarmadatiCache> products, string xmlPath, CancellationToken cancellation)
        {
            foreach (var record in ReadRecords(xmlPath))
            {
                cancellation.ThrowIfCancellationRequested();

                try
                {
                    if (!record.TryGetValue("FDI_0001", out var code))
                        continue;

                    products[code] = new FarmadatiCache
                    {
                        Aic = code,
                        Name = record.GetValueOrDefault("FDI_0004") ?? "",
                        ShortDescription = record.GetValueOrDefault("FDI_1702") ?? "",
                        LongDescription = record.GetValueOrDefault("FDI_4875") ?? "",
                        MacroGroup = "O",
                        CachedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        DatasetDate = DateTime.UtcNow
                    };

                    _logger.LogInformation("Loaded product TE006: {Aic} - {Name}", code, record.GetValueOrDefault("FDI_0004") ?? "");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante il caricamento del record TE006 con AIC {Aic}", record.GetValueOrDefault("FDI_0001") ?? "N/A");
                    throw;
                }
            }
        }
        private void LoadTe011(Dictionary<string, FarmadatiCache> products, string xmlPath, CancellationToken cancellation)
        {
            foreach (var record in ReadRecords(xmlPath))
            {
                cancellation.ThrowIfCancellationRequested();

                try
                {
                    if (!record.TryGetValue("FDI_0001", out var code))
                        continue;

                    products[code] = new FarmadatiCache
                    {
                        Aic = code,
                        Name = record.GetValueOrDefault("FDI_0004") ?? "",
                        MacroGroup = "V",
                        CachedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        DatasetDate = DateTime.UtcNow
                    };
                    _logger.LogInformation("Loaded product TE011: {Aic} - {Name}", code, record.GetValueOrDefault("FDI_0004") ?? "");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante il caricamento del record TE011 con AIC {Aic}", record.GetValueOrDefault("FDI_0001") ?? "N/A");
                    throw;
                }
            }
        }
        private void LoadTe015(Dictionary<string, FarmadatiCache> products, string xmlPath, CancellationToken cancellation)
        {
            foreach (var record in ReadRecords(xmlPath))
            {
                cancellation.ThrowIfCancellationRequested();

                try 
                {               
                 
                    if (!record.TryGetValue("FDI_0001", out var code))
                        continue;

                    products[code] = new FarmadatiCache
                    {
                        Aic = code,
                        Name = record.GetValueOrDefault("FDI_0004") ?? "",
                        MacroGroup = "V",
                        CachedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        DatasetDate = DateTime.UtcNow
                    };
                    _logger.LogInformation("Loaded product TE015: {Aic} - {Name}", code, record.GetValueOrDefault("FDI_0004") ?? "");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante il caricamento del record TE015 con AIC {Aic}", record.GetValueOrDefault("FDI_0001") ?? "N/A");
                    throw;
                }

            }
        }
    }
}
