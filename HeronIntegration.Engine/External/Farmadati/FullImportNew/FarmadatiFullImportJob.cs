using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using ServiceReference1;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace HeronIntegration.Engine.External.Farmadati.FullImportNew
{
    public class FarmadatiFullImportJob : IFarmadatiFullImportJob
    {
        private readonly IFarmadatiCacheRepository _cache;
        private readonly ImageStorageService _imageStorage;
        private readonly FarmadatiImageDownloader _imageDownloader;
        private readonly FarmadatiItaliaWebServicesM2Client _client;
        private readonly IFarmadatiUpdatesRepository _updatesRepository;

        private readonly string _username;
        private readonly string _password;

    public FarmadatiFullImportJob(
        IFarmadatiCacheRepository cache,
        ImageStorageService imageStorage,
        FarmadatiImageDownloader imageDownloader,
        IFarmadatiUpdatesRepository updatesRepository,
        IConfiguration configuration)
    {
        _cache = cache;

        _imageStorage = imageStorage;
        _imageDownloader = imageDownloader;
        _updatesRepository = updatesRepository;

        _client = new FarmadatiItaliaWebServicesM2Client(
            FarmadatiItaliaWebServicesM2Client.EndpointConfiguration
                .BasicHttpBinding_FarmadatiItaliaWebServicesM2);

        _username = configuration["Farmadati:Username"]!;
        _password = configuration["Farmadati:Password"]!;
    }

        public async Task ExecuteAsync()
        {
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

            try
            {
                // DOWNLOAD

                var te001Folder = await DownloadDatasetAsync("TE001");
                var te002Folder = await DownloadDatasetAsync("TE002");
                var te006Folder = await DownloadDatasetAsync("TE006");
                var te011Folder = await DownloadDatasetAsync("TE011");
                var te015Folder = await DownloadDatasetAsync("TE015");

                var te008Folder = await DownloadDatasetAsync("TE008");
                var tr039Folder = await DownloadDatasetAsync("TR039");
                var tr036Folder = await DownloadDatasetAsync("TR036");

                var te004Folder = await DownloadDatasetAsync("TE004");
                var te009Folder = await DownloadDatasetAsync("TE009");

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

                LoadTe001(products, te001Xml);
                LoadTe002(products, te002Xml);
                LoadTe006(products, te006Xml);
                LoadTe011(products, te011Xml);
                LoadTe015(products, te015Xml);

                int totalProducts = products.Count;
                int worked = 0;

                await _updatesRepository.UpdateProgressAsync(
                    updateId!,
                    totalProducts,
                    0,
                    "Inizio import", null);

                foreach (var batch in products.Values.Chunk(10000))
                {
                    await _cache.BulkUpsertAsync(batch);

                    worked += batch.Count();

                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        worked,
                        "Import in corso", null);
                }

                await _updatesRepository.UpdateProgressAsync(
                    updateId!,
                    totalProducts,
                    totalProducts,
                    "Import completato", null);

                products.Clear();

                // IMMAGINI ESISTENTI

                var existingFiles =
                    await _imageStorage.GetAllFilesAsync();

                _ = Task.Run(async () =>
                {
                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        totalProducts,
                        "Inserimento descrizione lunga", null);

                    await MergeTe008Async(te008Xml);

                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        totalProducts,
                        "Inserimento descrizione breve", null);
                    await MergeTr039Async(tr039Xml);

                    await _updatesRepository.UpdateProgressAsync(
                       updateId!,
                       totalProducts,
                       totalProducts,
                       "Inserimento codice macro group", null);
                    await MergeTr036Async(tr036Xml);

                    await _updatesRepository.UpdateProgressAsync(
                       updateId!,
                       totalProducts,
                       totalProducts,
                       "Inserimento immagini", null);
                    await MergeTe004Async(
                        te004Xml,
                        existingFiles);

                    await MergeTe009Async(
                        te009Xml,
                        existingFiles);

                    await _updatesRepository.UpdateProgressAsync(
                        updateId!,
                        totalProducts,
                        totalProducts,
                        "Completed", DateTime.Now);
                });
                
            }
            catch (Exception ex)
            {
                await _updatesRepository.UpdateProgressAsync(
                    updateId!,
                    0,
                    0,
                    "Error", DateTime.Now);

                throw;
            }
        }

        private async Task MergeTe004Async(
            string xmlPath,
            Dictionary<string, ObjectId> existingFiles)
        {
            var updates =
                new List<WriteModel<FarmadatiCache>>();

            foreach (var record in ReadRecords(xmlPath))
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

            if (updates.Count > 0)
            {
                await _cache.BulkWriteAsync(
                    updates);
            }
        }

        private async Task MergeTe009Async(
            string xmlPath,
            Dictionary<string, ObjectId> existingFiles)
        {
            var updates =
                new List<WriteModel<FarmadatiCache>>();

            foreach (var record in ReadRecords(xmlPath))
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

            if (updates.Count > 0)
            {
                await _cache.BulkWriteAsync(
                    updates);
            }
        }

        private async Task MergeTe008Async(string xmlPath)
        {
            var updates = new List<WriteModel<FarmadatiCache>>();

            foreach (var record in ReadRecords(xmlPath))
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
                    await _cache.BulkWriteAsync(updates);
                    updates.Clear();
                }
            }

            if (updates.Count > 0)
            {
                await _cache.BulkWriteAsync(updates);
            }
        }

        private async Task MergeTr039Async(string xmlPath)
        {
            var updates = new List<WriteModel<FarmadatiCache>>();

            foreach (var record in ReadRecords(xmlPath))
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
                    await _cache.BulkWriteAsync(updates);
                    updates.Clear();
                }
            }

            if (updates.Count > 0)
            {
                await _cache.BulkWriteAsync(updates);
            }
        }

        private async Task MergeTr036Async(string xmlPath)
        {
            var updates = new List<WriteModel<FarmadatiCache>>();

            foreach (var record in ReadRecords(xmlPath))
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
                    await _cache.BulkWriteAsync(updates);
                    updates.Clear();
                }
            }

            if (updates.Count > 0)
            {
                await _cache.BulkWriteAsync(updates);
            }
        }

        private async Task<string> DownloadDatasetAsync(string datasetCode)
        {
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
                "FarmadatiTemp");

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

        private void LoadTe001(Dictionary<string, FarmadatiCache> products, string xmlPath)
        {
            foreach (var record in ReadRecords(xmlPath))
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
            }
        }
        private void LoadTe002(Dictionary<string, FarmadatiCache> products, string xmlPath)
        {
            foreach (var record in ReadRecords(xmlPath))
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
            }
        }
        private void LoadTe006(Dictionary<string, FarmadatiCache> products, string xmlPath)
        {
            foreach (var record in ReadRecords(xmlPath))
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
            }
        }
        private void LoadTe011(Dictionary<string, FarmadatiCache> products, string xmlPath)
        {
            foreach (var record in ReadRecords(xmlPath))
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
            }
        }
        private void LoadTe015(Dictionary<string, FarmadatiCache> products, string xmlPath)
        {
            foreach (var record in ReadRecords(xmlPath))
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
            }
        }
    }
}
