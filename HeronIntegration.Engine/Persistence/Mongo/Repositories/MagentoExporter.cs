using FluentFTP;
using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Renci.SshNet;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;

public class MagentoExporter : IMagentoExporter
{
    private readonly HttpClient _http;
    private readonly ImageStorageService _imageStorage;
    private readonly IExportRepository _exportRepo;
    private readonly MagentoConfig _magento;
    private readonly IBatchRepository _batchRepo;
    private readonly IHostEnvironment _env;
    private readonly ICustomerRepository _customerRepo;
    private readonly ICustomerMagentoCategoriesRepository _customerMagentoCategoriesRepository;
    private readonly GridFSBucket _gridFsBucket;
    private string BaseUrl => _magento.BaseUrl.TrimEnd('/');
    private readonly IImportToMagentoStatusRepository _importToMagento;
    private readonly IMongoDatabase _database;
    private const int MaxParallel = 20;

    public MagentoExporter(
        HttpClient http,
        ImageStorageService imageStorage,
        IExportRepository exportRepo,
        MagentoConfig magento,
        IBatchRepository batchRepo,
        ICustomerRepository customerRepo,
        IHostEnvironment env,
        ICustomerMagentoCategoriesRepository customerMagentoCategoriesRepository,
        IImportToMagentoStatusRepository importToMagento,
        IMongoDatabase database)
    {
        _http = http;
        _imageStorage = imageStorage;

        _http.Timeout = TimeSpan.FromMinutes(10);
        _exportRepo = exportRepo;
        _magento = magento;
        _batchRepo = batchRepo;
        _customerRepo = customerRepo;

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _magento.Token);

        _http.DefaultRequestVersion = HttpVersion.Version20;
        _http.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        _env = env;
        _customerMagentoCategoriesRepository = customerMagentoCategoriesRepository;
        _importToMagento = importToMagento;
        _database = database;

        _gridFsBucket = new GridFSBucket(database);
    }

    // =====================================================
    // Single product export path. The implementation uses PUT upsert.
    // =====================================================
    public async Task<MagentoInsertResult> ExportAsync(ResolvedProduct p, CancellationToken token)
    {
        var result = new MagentoInsertResult();

        try
        {
            await UpsertProductAsync(p, p.BatchId.ToString(), token);
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    // =====================================================
    // 🚀 IMPORT MASSIVO PARALLELO (NUOVO)
    // =====================================================
    public async Task ImportProductsAsync(IEnumerable<ResolvedProduct> products, CancellationToken token)
    {
        try
        {
            var l = products.ToList();

            var batchId = l[0].BatchId.ToString();
            var b = await _batchRepo.GetByIdAsync(batchId);
            if (b == null)
                return;

            var c = await _customerRepo.GetByIdAsync(b.CustomerId);
            if (c == null)
                return;

            // Start dashboard progress for product import.
            await _importToMagento.UpdateImportStatusAsync(batchId, totalProductsToInsert: l.Count, insertProductsStatus: OperationsStatus.Running);

            //INVIO UNO PER UNO
            if (!c.Msi)
            {
                int elem = 1000;
                var importedSkus = new ConcurrentBag<string>();

                var chunks = l.Chunk(elem);

                await Parallel.ForEachAsync(
                    chunks,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 3,
                        CancellationToken = token
                    },
                    async (chunk, ct) =>
                    {
                        var result =
                            await UpsertProductCustomBulkAsync(
                                chunk.ToList(),
                                batchId,
                                ct
                            );

                        if (result?.Items == null)
                            return;

                        foreach (var item in result.Items)
                        {
                            if (
                                item.Success &&
                                (
                                    item.InsertType == 1 ||
                                    item.InsertType == 2
                                )
                            )
                            {
                                importedSkus.Add(item.Sku);
                                await _exportRepo.SetStatusAsync(batchId, item.Sku, ExportStatus.Insert);
                            }
                            if(!item.Success)
                                await _exportRepo.SetErrorAsync(batchId, item.Sku, item.Message);
                        }
                        // Update dashboard progress for processed product chunks.
                        await _importToMagento.UpdateImportStatusAsync(batchId, totalProductsInserted: elem);
                    });

                // Mark product import as completed.
                await _importToMagento.UpdateImportStatusAsync(batchId, insertProductsStatus: OperationsStatus.Ended);
            }

            else
                //INVIO BULK
                await UpsertProductBulkAsync(l, batchId, token);
        }
        catch (OperationCanceledException)
        {
            // cancellazione batch → uscita pulita
        }
    }

    // =====================================================
    // 🔥 REINDEX/CRON SU CUSTOM API
    // =====================================================
    public async Task ReindexAsync(
        List<string> skus,
        string batchId,
        CancellationToken token)
    {
        if (
            skus == null ||
            skus.Count == 0
        )
        {
            return;
        }

        try
        {
            var request = new
            {
                batchId = batchId,
                skus = skus
            };

            var json =
                System.Text.Json.JsonSerializer.Serialize(
                    request
                );

            using var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );

            using var response =
                await _http.PostAsync(
                    $"{BaseUrl}/rest/V1/heron/reindex",
                    content,
                    token
                );

            var responseContent =
                await response.Content
                    .ReadAsStringAsync(token);

            if (!response.IsSuccessStatusCode)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    // =====================================================
    // 🔥 POLLING SU CUSTOM API
    // =====================================================
    public async Task WaitReindexAsync(string batchId, CancellationToken token)
    {
        decimal? lastPercent = null;
        DateTime lastChange = DateTime.UtcNow;
        DateTime? firstErrorTime = null;

        const int timeoutMinutes = 5;
        while (true)
        {
            try
            {
                var response = await _http.GetStringAsync(
                    $"{BaseUrl}/rest/V1/heron/reindex-status/{batchId}",
                    token);

                // Se la chiamata va a buon fine azzero il timer degli errori
                firstErrorTime = null;

                var innerJson =
                    System.Text.Json.JsonSerializer.Deserialize<string>(response);

                var result =
                    System.Text.Json.JsonSerializer.Deserialize<ReindexStatus>(
                        innerJson!,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                if (result != null)
                {
                    //Console.WriteLine($"{result.Percent}%");

                    // Verifica se la percentuale è cambiata
                    if (lastPercent == null || Math.Abs(lastPercent.Value - result.Percent) > 0.01m)
                    {
                        lastPercent = result.Percent;
                        lastChange = DateTime.UtcNow;
                    }
                    else
                    {
                        // Se è ferma da troppo tempo
                        if (DateTime.UtcNow - lastChange > TimeSpan.FromMinutes(timeoutMinutes))
                        {
                            Console.WriteLine(
                                $"Reindex fermo al {result.Percent}% da oltre {timeoutMinutes} minuti. Forzo al 100%.");

                            await _importToMagento.UpdateImportStatusAsync(
                                batchId,
                                reindexPercent: 100);

                            break;
                        }
                    }

                    // Aggiornamento normale
                    await _importToMagento.UpdateImportStatusAsync(
                        batchId,
                        reindexPercent: result.Percent);

                    if (!result.Running || result.Percent >= 98)
                    {
                        await _importToMagento.UpdateImportStatusAsync(
                            batchId,
                            reindexPercent: 100);

                        break;
                    }
                }

                await Task.Delay(5000, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                if (firstErrorTime == null)
                {
                    firstErrorTime = DateTime.UtcNow;
                }

                if (DateTime.UtcNow - firstErrorTime >
                    TimeSpan.FromMinutes(timeoutMinutes))
                {
                    Console.WriteLine(
                        $"Errore continuo da oltre {timeoutMinutes} minuti. Forzo il reindex al 100%.");

                    await _importToMagento.UpdateImportStatusAsync(
                        batchId,
                        reindexPercent: 100);

                    break;
                }
            }
        }

    }
    // =====================================================
    // 🔥 POLLING IMMAGINI SU CUSTOM API
    // =====================================================
    public async Task WaitPollingImagesAsync(string batchId, CancellationToken token)
    {
        DateTime lastChange = DateTime.UtcNow;
        DateTime? firstErrorTime = null;

        const int timeoutMinutes = 5;
        int processedCount = 0;

        while (true)
        {
            try
            {
                var response =
                await _http.GetStringAsync(
                    $"{BaseUrl}/rest/V1/heron/images-status/{batchId}",
                    token
                );

                // Se la chiamata va a buon fine azzero il timer degli errori
                firstErrorTime = null;

                var innerJson =
                    System.Text.Json.JsonSerializer.Deserialize<string>(response);

                var result =
                    System.Text.Json.JsonSerializer.Deserialize<ImagesImportStatus>(
                        innerJson!,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }
                    );

                var allSkus = result!.Inserted;

                var newSkus = allSkus.Skip(processedCount).ToList();
                processedCount = allSkus.Count;

                if (newSkus.Count > 0)
                {
                    var l = new List<InventoryItem>();
                    foreach (var s in newSkus)
                    {
                        var i = new InventoryItem()
                        {
                            Id = batchId,
                            Qty = 0,
                            Message = "Inserimento riuscito",
                            Sku = s
                        };
                        l.Add(i);
                    }
                    await _exportRepo.SetStatusBulkAsync(
                        l,
                        ExportStatus.InsertImages
                    );

                    // Update image import progress with newly confirmed SKUs.
                    await _importToMagento.UpdateImportStatusAsync(batchId, totalImagesInserted: l.Count);
                }


                if (result != null)
                {
                    Console.WriteLine(
                        $"{result.Percent}%"
                    );

                    if (!result.Running)
                    {
                        // Mark image import as completed when Magento reports no running work.
                        await _importToMagento.UpdateImportStatusAsync(batchId, insertImagesStatus: OperationsStatus.Ended);
                        break;
                    }
                }

                await Task.Delay(
                    2000,
                    token
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                if (firstErrorTime == null)
                {
                    firstErrorTime = DateTime.UtcNow;
                }

                if (DateTime.UtcNow - firstErrorTime >
                    TimeSpan.FromMinutes(timeoutMinutes))
                {
                    Console.WriteLine(
                        $"Errore continuo da oltre {timeoutMinutes} minuti. Forzo il reindex al 100%.");

                    await _importToMagento.UpdateImportStatusAsync(batchId, insertImagesStatus: OperationsStatus.Ended);

                    break;
                }
            }
        }
    }

    // =====================================================
    // 🔥 CLEAN INDEX SU CUSTOM API
    // =====================================================
    public async Task CleanIndex(CancellationToken token)
    {
        var response =
            await _http.PostAsync(
                $"{BaseUrl}/rest/V1/heron/clean-index",
                null,
                token
            );

        Console.WriteLine(response);
    }

    // =====================================================
    // 🔥 CLEAN CACHE SU CUSTOM API
    // =====================================================
    public async Task CleanCache(CancellationToken token)
    {
        var response =
            await _http.PostAsync(
                $"{BaseUrl}/rest/V1/heron/clean-cache",
                null,
                token
            );

        Console.WriteLine(response);
    }

    // =====================================================
    // 🔥 ELIMINA TUTTI I PRODOTTI SU CUSTOM API
    // =====================================================
    public async Task DeleteProducts()
    {
        var response =
            await _http.PostAsync(
                $"{BaseUrl}/rest/V1/heron/delete-products", null
            );

        Console.WriteLine(response);
    }


    // =====================================================
    // 🔥 UPSERT PRODOTTO API CUSTOM
    // =====================================================
    private async Task<MagentoImportResponse?>
        UpsertProductCustomBulkAsync(
            List<ResolvedProduct> products,
            string batchId,
            CancellationToken token)
    {
        try { 
        var mapped =
            products
                .Select(MapMagentoProduct)
                .ToList();

        var request = new
        {
            products = System.Text.Json.JsonSerializer.Serialize(
                mapped
            )
        };

            var pr = mapped.Where(a => a.macroGroup != null).ToList();
           foreach(var o in pr)
            {
                if (o.macroGroup == "P")
                    Console.WriteLine("ciao");
            }

        var json =
            System.Text.Json.JsonSerializer.Serialize(request);

        using var content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json"
            );

        using var response =
            await _http.PostAsync(
                $"{BaseUrl}/rest/V1/heron/import-products",
                content,
                token
            );

        var responseContent =
            await response.Content
                .ReadAsStringAsync(token);

        response.EnsureSuccessStatusCode();

        var jsonString =
        System.Text.Json.JsonSerializer.Deserialize<string>(
            responseContent
        );

        return System.Text.Json.JsonSerializer.Deserialize
            <MagentoImportResponse>(
                jsonString!,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );
        }
        catch 
        {

        }
        return null;
    }

    // =====================================================
    // 🔥 UPSERT QUANTITA' PRODOTTO API CUSTOM
    // =====================================================
    private async Task<MagentoImportResponse?>
        UpdateQtyProductCustomBulkAsync(
            List<InventoryItem> products,
            string batchId,
            CancellationToken token)
    {
        var mapped =
            products
                .Select(MapMagentoQtyProduct)
                .ToList();

        var request = new
        {
            products = System.Text.Json.JsonSerializer.Serialize(
                mapped
            )
        };

        var json =
            System.Text.Json.JsonSerializer.Serialize(request);

        using var content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json"
            );

        using var response =
            await _http.PostAsync(
                $"{BaseUrl}/rest/V1/heron/update-qty",
                content,
                token
            );

        var responseContent =
            await response.Content
                .ReadAsStringAsync(token);

        response.EnsureSuccessStatusCode();

        var jsonString =
        System.Text.Json.JsonSerializer.Deserialize<string>(
            responseContent
        );

        return System.Text.Json.JsonSerializer.Deserialize
            <MagentoImportResponse>(
                jsonString!,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );
    }


    // =====================================================
    // 🔥 UPSERT IMMAGINI API CUSTOM
    // =====================================================
    private async Task UploadImagesBulkAsync(
    List<ResolvedProduct> products,
    CancellationToken token)
    {
        try
        {
            /*
            |--------------------------------------------------------------------------
            | PAYLOAD
            |--------------------------------------------------------------------------
            */

            var payload =
                new List<object>();

            foreach (var product in products)
            {
                try
                {
                    if (
                        product.Images == null ||
                        !product.Images.Any()
                    )
                    {
                        continue;
                    }

                    var imgs = await MapMagentoImagesAsync(product, token);

                    var images =
                        new List<object>();

                    foreach (var img in imgs.images)
                    {
                        try { 
                            images.Add(
                                new
                                {
                                    name = img.name,
                                    base64 = img.base64
                                });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                    if (!images.Any())
                        continue;

                    payload.Add(
                        new
                        {
                            sku = product.Aic,
                            images = images
                        });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            /*
            |--------------------------------------------------------------------------
            | EMPTY
            |--------------------------------------------------------------------------
            */

            if (!payload.Any())
                return;

            /*
            |--------------------------------------------------------------------------
            | JSON
            |--------------------------------------------------------------------------
            */

            var json =
                System.Text.Json.JsonSerializer
                    .Serialize(
                        new
                        {
                            items =
                                System.Text.Json.JsonSerializer
                                    .Serialize(payload)
                        });

            using var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );

            /*
            |--------------------------------------------------------------------------
            | REQUEST
            |--------------------------------------------------------------------------
            */

            using var response =
                await _http.PostAsync(
                    $"{BaseUrl}/rest/V1/heron/images",
                    content,
                    token
                );

            var responseContent =
                await response.Content
                    .ReadAsStringAsync(token);

            /*
            |--------------------------------------------------------------------------
            | RESPONSE
            |--------------------------------------------------------------------------
            */

            if (!response.IsSuccessStatusCode)
            {
               
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    // =====================================================
    // 🔥 MAP PRODOTTO MAGENTO API CUSTOM
    // =====================================================
    private MagentoBulkProduct MapMagentoProduct(
    ResolvedProduct x)
    {
        var specialPrice =
            x.OriginalPrice > x.Price
                ? x.Price
                : 0;

        return new MagentoBulkProduct
        {
            sku = x.Aic,

            name = x.Name,

            description =
                x.LongDescription ?? string.Empty,

            short_description =
                x.ShortDescription ?? string.Empty,

            price =
                x.OriginalPrice > 0
                    ? x.OriginalPrice
                    : x.Price,

            special_price = specialPrice,

            qty = x.Availability,

            status = 1,

            visibility = 4,

            attribute_set_id = 4,

            type_id = "simple",

            manufacturer = Convert.ToInt32(x.Producer),

            supplier = x.SupplierCode!,

            weight = x.Weight,

            vat = x.Vat,

            macroGroup = x.MacroGroup,

            website_ids = new List<int>
            {
                1
            },

            category_ids =
                x.MagentoCategoryId.HasValue
                    ? new List<int>
                    {
                    x.MagentoCategoryId.Value
                    }
                    : new List<int>()
        };
    }

    // =====================================================
    // 🔥 MAP QUANTITA' PRODOTTO MAGENTO API CUSTOM
    // =====================================================
    private MagentoBulkQty MapMagentoQtyProduct(
    InventoryItem x)
    {
        return new MagentoBulkQty
        {
            sku = x.Sku,
            qty = x.Qty,
            //qty = 50
        };
    }

    // =====================================================
    // 🔥 MAP IMMAGINI MAGENTO API CUSTOM
    // =====================================================
    private async Task<MagentoImageRequest>
    MapMagentoImagesAsync(
        ResolvedProduct product,
        CancellationToken token)
    {
        var result =
            new MagentoImageRequest
            {
                sku = product.Aic
            };

        if (
            product.Images == null ||
            product.Images.Count == 0
        )
        {
            return result;
        }

        foreach (var image in product.Images)
        {
            try
            {
                if (image.GridFsId == null)
                    continue;

                /*
                |--------------------------------------------------------------------------
                | GRIDFS DOWNLOAD
                |--------------------------------------------------------------------------
                */

                var bytes =
                    await _imageStorage
                    .GetBase64Async(
                        (MongoDB.Bson.ObjectId)
                        image.GridFsId
                    );

                if (
                    bytes == null ||
                    bytes.Length == 0
                )
                {
                    continue;
                }

                /*
                |--------------------------------------------------------------------------
                | BASE64
                |--------------------------------------------------------------------------
                */


                result.images.Add(
                    new MagentoImageItem
                    {
                        name =
                            image.AltText
                            ?? $"{Guid.NewGuid()}.jpg",

                        base64 = bytes
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        return result;
    }

    private async Task ImportByCsvAsync(
    List<ResolvedProduct> products,
    Customer customer,
    BatchExecution b,
    CancellationToken token)
    {
        var file = await GenerateCsvAsync(products, b.Id.ToString(), customer.Id, token);

        var finalFile = await ZipIfNeededAsync(file, token);

        await UploadFtpAsync(finalFile, customer!, token);

        var processId = await LaunchMagentoImportAsync(customer!, b.Id.ToString(), token);

        await _batchRepo.UpdatProcessId(b.Id.ToString(), processId);

        await PollImportStatusAsync(customer, b.Id.ToString(), token, products.Count());
    }

    private async Task<string> GenerateCsvAsync(
        List<ResolvedProduct> products,
        string batchId,
        string customerId,
        CancellationToken token)
    {
        var root = _env.ContentRootPath;
        var parent = Directory.GetParent(root)!.FullName;

        var file = Path.Combine(
            parent,
            "Export",
            $"magento_import_{batchId}.csv"
        );

        Directory.CreateDirectory(
            Path.GetDirectoryName(file)!
        );

        var encoding = new UTF8Encoding(false);

        await using var stream = new FileStream(
            file,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None
        );

        await using var sw = new StreamWriter(stream, encoding);

        // HEADER
        await sw.WriteLineAsync(
            "sku,store_view_code,attribute_set_code,product_type,category_ids," +
            "product_websites,name,description,short_description,weight,status," +
            "visibility,price,special_price,special_from_date,special_to_date," +
            "tax_class_name,qty,is_in_stock,manufacturer,ean,supplier,url_key"
        );

        foreach (var p in products
            .GroupBy(x => x.Aic)
            .Select(x => x.First()))
        {
            token.ThrowIfCancellationRequested();

            var normalPrice = p.OriginalPrice == 0
                ? p.Price
                : p.OriginalPrice;

            var specialPrice = p.OriginalPrice > p.Price
                ? p.Price.ToString(CultureInfo.InvariantCulture)
                : "";

            var categoryIds = p.MagentoCategoryId?.ToString() ?? "";

            var isInStock = p.Availability > 0 ? "1" : "0";

            // 🔥 URL KEY SICURA
            var cleanUrlKey = Regex.Replace(
                (p.Name ?? "").ToLowerInvariant(),
                @"[^a-z0-9]+",
                "-"
            );

            cleanUrlKey = Regex.Replace(cleanUrlKey, @"-+", "-")
                .Trim('-');

            cleanUrlKey = $"{cleanUrlKey}-{p.Aic}";

            // 🔥 HTML SAFE
            var description = CleanText(
                p.LongDescription ??
                p.ShortDescription ??
                p.Name
            );

            var shortDescription = CleanText(
                p.ShortDescription ??
                p.Name
            );

            var name = CleanText(
                p.Name?.Replace("*", " ")
            );

            var row = string.Join(",",
                Csv(p.Aic),                                             // sku
                Csv(""),                                                 // store_view_code
                Csv("Default"),                                         // attribute_set_code
                Csv("simple"),                                          // product_type
                Csv(categoryIds),                                       // categories
                Csv("base"),                                            // websites
                Csv(name),                                              // name
                Csv(description),                                       // description
                Csv(shortDescription),                                  // short_description
                Csv("1"),                                               // weight
                Csv("1"),                                               // status
                Csv("4"),                                               // visibility
                Csv(normalPrice.ToString(CultureInfo.InvariantCulture)),
                Csv(specialPrice),
                Csv(""),
                Csv(""),
                Csv("Taxable Goods"),
                Csv(p.Availability.ToString(CultureInfo.InvariantCulture)),
                Csv(isInStock),
                Csv(CleanText(p.Producer)),
                Csv(p.Aic),
                Csv(CleanText(p.SupplierCode)),
                Csv(cleanUrlKey)
            );

            await sw.WriteLineAsync(row);
        }

        return file;
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "\"\"";

        value = value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();

        // escape doppi apici
        value = value.Replace("\"", "\"\"");

        return $"\"{value}\"";
    }

    private static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.Replace("\0", "");

        // 🔥 mantiene HTML ma pulisce caratteri invalidi
        value = Regex.Replace(value, @"[\u0000-\u001F]+", " ");

        value = Regex.Replace(value, @"\s+", " ");

        return value.Trim();
    }

    private async Task UploadFtpAsync(
    string localFile,
    Customer customer,
    CancellationToken token, 
    string rm = "/var/import")
    {
        var host = customer.Magento!.FtpHost;
        var user = customer.Magento.FtpUser;
        var pass = customer.Magento.FtpPassword;
        var remoteFolder =
                customer.Magento.MagentoRootPath.TrimEnd('/') + rm;

        var remoteFile =
            remoteFolder + "/" + Path.GetFileName(localFile);

        var isSftp = await IsSftpAsync(host, token);

        if (isSftp)
        {
            using var sftp = new Renci.SshNet.SftpClient(
                host,
                22,
                user,
                pass);

            sftp.Connect();

            if (!sftp.IsConnected)
                throw new Exception("Connessione SFTP fallita");

            using var fs = File.OpenRead(localFile);

            EnsureSftpDirectoryExists(sftp, remoteFolder);

            try
            {

                var files = sftp.ListDirectory(remoteFolder);

                foreach (var file in files)
                {
                    if (file.Name == "." || file.Name == "..")
                        continue;

                    var fullPath = remoteFolder + "/" + file.Name;

                    if (!file.IsDirectory)
                        sftp.DeleteFile(fullPath);
                }

                sftp.UploadFile(fs, remoteFile, true);
            }
            catch(Exception e)
            {
                var ex = e;
            }

            sftp.Disconnect();
        }
        else
        {
            using var ftp = new FluentFTP.FtpClient(
                host,
                user,
                pass);

            ftp.Connect();

            if (!ftp.IsConnected)
                throw new Exception("Connessione FTP fallita");

            ftp.UploadFile(
                localFile,
                remoteFile,
                FtpRemoteExists.Overwrite,
                true);

            ftp.Disconnect();
        }
    }

    private void EnsureSftpDirectoryExists(
    Renci.SshNet.SftpClient sftp,
    string path)
    {
        var parts = path.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        var current = "/";

        foreach (var part in parts)
        {
            current += part + "/";

            if (!sftp.Exists(current))
            {
                sftp.CreateDirectory(current);
            }
        }
    }

    private async Task<bool> IsSftpAsync(
    string host,
    CancellationToken token)
    {
        try
        {
            using var tcp = new TcpClient();

            var connectTask = tcp.ConnectAsync(host, 22);

            var completed = await Task.WhenAny(
                connectTask,
                Task.Delay(3000, token));

            if (completed != connectTask)
                return false;

            using var stream = tcp.GetStream();

            var buffer = new byte[256];

            var readTask = stream.ReadAsync(buffer, 0, buffer.Length, token);

            completed = await Task.WhenAny(
                readTask,
                Task.Delay(3000, token));

            if (completed != readTask)
                return false;

            var banner = Encoding.ASCII.GetString(
                buffer,
                0,
                readTask.Result);

            return banner.StartsWith("SSH-");
        }
        catch
        {
            return false;
        }
    }

    private async Task<int> LaunchMagentoImportAsync(
        Customer customer,
        string batchId,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        using var client = new SshClient(
            customer.Magento!.FtpHost,
            customer.Magento.FtpUser,
            customer.Magento.FtpPassword);

        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);

        client.Connect();

        if (!client.IsConnected)
            throw new Exception("Connessione SSH fallita.");

        /*
         * evita doppio avvio stesso batch
         */
        var checkCmd = client.CreateCommand(
            $"pgrep -f \"heron:import\"");

        checkCmd.CommandTimeout = TimeSpan.FromSeconds(5);

        var running = checkCmd.Execute().Trim();

        if (!string.IsNullOrWhiteSpace(running))
            throw new Exception(
                $"Import Magento già in esecuzione per batch {batchId}");

        var logFolder = "var/log/heron";

        /*
         * avvio detached + ritorno PID immediato
         */
        var pidFile = $"{logFolder}/{batchId}.pid";

        var startCmd =
            $"cd {customer.Magento.MagentoRootPath} && " +
            $"mkdir -p {logFolder} && " +
            $"sh -c 'nohup php bin/magento heron:import " +
            $"> {logFolder}/{batchId}.log 2>&1 < /dev/null & echo $! > {pidFile}'";

            client.RunCommand(startCmd);

            await Task.Delay(1000, token);
            var pidText = client.RunCommand(
                $"cd {customer.Magento.MagentoRootPath} && cat {pidFile}")
                .Result.Trim();


        client.Disconnect();

        if (string.IsNullOrWhiteSpace(pidText))
            throw new Exception(
                "Processo Magento non avviato correttamente.");

        return int.Parse(pidText);
    }

    private async Task PollImportStatusAsync(
        Customer customer,
        string batchId,
        CancellationToken token, 
        int count)
    {
        int processedCount = 0;

        while (!token.IsCancellationRequested)
        {
            var status = GetImportLogAsync(
                customer,
                batchId,
                token);

            if (status == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                continue;
            }

            var content = GetSkuFileAsync(
                customer,
                batchId,
                token);

            if (!string.IsNullOrWhiteSpace(content) && !content.Contains("EMPTY"))
            {
                var allSkus = content
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => Newtonsoft.Json.JsonConvert.DeserializeObject<ImportSkuStatus>(x))
                    .Where(x => x != null)
                    .ToList();

                // Read only the SKUs not processed by previous polling iterations.
                var newSkus = allSkus.Skip(processedCount).ToList();
                processedCount = allSkus.Count;

                if (newSkus.Count > 0)
                {
                    var grouped = newSkus
                     .GroupBy(x => x!.Status)
                     .ToDictionary(
                         g => g.Key,
                         g => g.Select(i => new InventoryItem
                         {
                             Id = batchId,
                             Qty = 0,
                             Message = i!.Error,
                             Sku = i!.Sku
                         }).ToList()
                     );

                    foreach (var group in grouped)
                    {
                        if (group.Value.Count == 0)
                            continue;

                        await _exportRepo.SetStatusBulkAsync(
                            group.Value,
                            group.Key
                        );
                    }
                }
            }

            Console.WriteLine($"Importati: {status.Imported} / Letti: {status.RowsRead}");

            if (status.Status == "completed" && processedCount >= count)
            {
                await _batchRepo.CloseAsync(batchId);
                return;
            }

            // 🔥 QUI: 2 SECONDI REALI
            await Task.Delay(TimeSpan.FromSeconds(2), token);
        }
    }

    private string GetSkuFileAsync(
        Customer customer,
        string batchId,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        using var client = new SshClient(
            customer.Magento!.FtpHost,
            customer.Magento.FtpUser,
            customer.Magento.FtpPassword);

        client.Connect();

        var file = $"var/log/heron/{batchId}.sku";

        var cmd =
            $"cd {customer.Magento.MagentoRootPath} && " +
            $"if [ -f {file} ]; then cat {file}; else echo EMPTY; fi";

        var result = client.RunCommand(cmd);

        client.Disconnect();

        return result.Result;
    }

    private ImportStatus GetImportLogAsync(
        Customer customer,
        string batchId,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        using var client = new SshClient(
            customer.Magento!.FtpHost,
            customer.Magento.FtpUser,
            customer.Magento.FtpPassword);

        client.Connect();

        var statusFile = $"var/log/heron/{batchId}.status.json";

        var cmd =
            $"cd {customer.Magento.MagentoRootPath} && " +
            $"if [ -f {statusFile} ]; then cat {statusFile}; else echo NOT_FOUND; fi";

        var result = client.RunCommand(cmd);

        client.Disconnect();

        if (result.Result.Contains("NOT_FOUND"))
            return new ImportStatus() { BatchId = batchId};

        var import = System.Text.Json.JsonSerializer.Deserialize<ImportStatus>(result.Result!,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;

        return import;
    }

    public async Task ReindexAllAsync(
    List<ResolvedProduct> products, string batchId, CancellationToken token)
    {

        await ReindexAsync(
            products.Select(a=>a.Aic).ToList(),
            batchId,
            token
        );
    }

    public async Task StopMagentoImportAsync(
    string batchId)
    {

        var b = await _batchRepo.GetByIdAsync(batchId);
        if (b == null)
            return;

        var customer = await _customerRepo.GetByIdAsync(b.CustomerId);
        if (customer == null)
            return;

        using var client = new SshClient(
            customer.Magento!.FtpHost,
            customer.Magento.FtpUser,
            customer.Magento.FtpPassword);

        client.Connect();

        client.RunCommand($"kill {b.ProcessId}");

        client.RunCommand(
            $"pkill -f \"heron:import {batchId}\"");

        client.Disconnect();

        await Task.CompletedTask;
    }

    private async Task<string> ZipIfNeededAsync(
    string file,
    CancellationToken token)
    {
        var fi = new FileInfo(file);

        if (fi.Length < 100_000_000)
            return file;

        var zip = Path.ChangeExtension(file, ".zip");

        using var archive = ZipFile.Open(zip, ZipArchiveMode.Create);

        archive.CreateEntryFromFile(file, Path.GetFileName(file));

        await Task.CompletedTask;

        return zip;
    }


    // =====================================================
    // 🔥 UPSERT PRODOTTO  
    // =====================================================
    private async Task UpsertProductAsync(ResolvedProduct p, string batchId, CancellationToken token)
    {
        var payload = new
        {
            product = BuildMagentoProductWithoutImages(p)
        };

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{BaseUrl}/rest/V1/products/{p.Aic}"
        );

        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        var j = System.Text.Json.JsonSerializer.Serialize(payload);

        await SendAsync(request, token);

        await UploadImagesAsync(p, token);

        await _exportRepo.SetStatusAsync(batchId, p.Aic, ExportStatus.Insert);
    }
    public async Task UpsertProductBulkAsync(List<ResolvedProduct> products, string batchId, CancellationToken token)
    {
        const int batchSize = 500;

        try
        {
            foreach (var batch in products.Chunk(batchSize))
            {
                var payload = batch.Select(p => new
                {
                    product = BuildMagentoProductWithoutImages(p)
                });

                var j = System.Text.Json.JsonSerializer.Serialize(payload);


                var req = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/rest/async/bulk/V1/products"
                );

                req.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                await SendAsync(req, token);

                var list = batch.Select(p => new InventoryItem
                {
                    Id = batchId,
                    Sku = p.Aic,
                    Qty = p.Availability
                }).ToList();

                await _exportRepo.SetStatusBulkAsync(list, ExportStatus.Insert);

            }
        }
        catch(Exception e) 
        { 
            Console.WriteLine(e.Message);
        }
    }


    // =====================================================
    // 🔥 UPDATE STOCK
    // =====================================================
    public async Task UpdateStockBulkAsync(List<InventoryItem> items, string batchId, CancellationToken token)
    {
        try
        {
            var l = items.ToList();

            var b = await _batchRepo.GetByIdAsync(batchId);
            if (b == null)
                return;

            var c = await _customerRepo.GetByIdAsync(b.CustomerId);
            if (c == null)
                return;

            //INVIO UNO PER UNO
            if (!c.Msi)
            {
                int elem = 1000;
                var importedSkus = new ConcurrentBag<string>();

                var chunks = l.Chunk(elem);

                await Parallel.ForEachAsync(
                    chunks,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 3,
                        CancellationToken = token
                    },
                    async (chunk, ct) =>
                    {
                        var result =
                            await UpdateQtyProductCustomBulkAsync(
                                chunk.ToList(),
                                batchId,
                                ct
                            );

                        if (result?.Items == null)
                            return;

                        foreach (var item in result.Items)
                        {
                            if (
                                item.Success &&
                                (
                                    item.InsertType == 1 ||
                                    item.InsertType == 2
                                )
                            )
                            {
                                importedSkus.Add(item.Sku);
                                await _exportRepo.SetStatusAsync(batchId, item.Sku, ExportStatus.UpdatePrice);
                            }
                            if (!item.Success)
                                await _exportRepo.SetErrorAsync(batchId, item.Sku, item.Message);
                        }

                        await _importToMagento.UpdateImportStatusAsync(batchId, totalProductsUpdated: elem);

                    });


                // Mark stock update as completed.
                await _importToMagento.UpdateImportStatusAsync(batchId, updateProductsStatus: OperationsStatus.Ended);

            }
            else
                //INVIO BULK
                await UpdateQuantityMsiAsync(items, token);

        }
        catch (OperationCanceledException)
        {
            // cancellazione → ignora
        }
    }
    private async Task UpdateQuantityMsiAsync(List<InventoryItem> items, CancellationToken token)
    {
        const int batchSize = 1000;

        foreach (var batch in items.Chunk(batchSize))
        {
            try
            {
                var payload = new
                {
                    sourceItems = batch.Select(i => new
                    {
                        sku = i.Sku,
                        source_code = "default",
                        quantity = i.Qty,
                        status = i.Qty > 0 ? 1 : 0
                    })
                };

                var req = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/rest/V1/inventory/source-items"
                );

                req.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                await SendAsync(req, token);

                await _exportRepo.SetStatusBulkAsync(batch.ToList(), ExportStatus.UpdatePrice);

            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                await _exportRepo.SetStatusBulkAsync(batch.ToList(), ExportStatus.Error);
            }
        }
    }
    private async Task UpdateQuantityAsync(string batchId, string sku, int qty, CancellationToken token)
    {
        try
        {
            var payload = new
            {
                stockItem = new
                {
                    qty = qty,
                    is_in_stock = qty > 0,
                    manage_stock = true,
                    use_config_manage_stock = false
                }
            };

            var req = new HttpRequestMessage(
                HttpMethod.Put,
                $"{BaseUrl}/rest/V1/products/{sku}/stockItems/1"
            );

            req.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            //var j = JsonSerializer.Serialize(payload);

            await SendAsync(req, token);


        }
        catch(Exception e)
        {
            var x = e;
        }
        await _exportRepo.SetStatusAsync(batchId, sku, ExportStatus.UpdatePrice);
    }


    // =====================================================
    // 🔥 UPDATE IMMAGINI
    // =====================================================
    public async Task UpdateImageBulkAsync(List<ResolvedProduct> items, CancellationToken token)
    {
        try
        {
            var b = await _batchRepo.GetByIdAsync(items[0].BatchId.ToString());
            if (b == null)
                return;

            var c = await _customerRepo.GetByIdAsync(b.CustomerId);
            if (c == null)
                return;

            //INVIO IMMAGINE UNO AD UNO
            if (!c.Msi)
            {
                var l = items.Where(a=>a.Images.Count > 0).ToList();

                var chunksImg = l.Chunk(20);

                await Parallel.ForEachAsync(
                    chunksImg,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 2,
                        CancellationToken = token
                    },
                    async (chunksImg, ct) =>
                    {
                        await UploadImagesBulkAsync(chunksImg.ToList(), token);

                    });
            }
            else
                await ImportImagesBulkAsync(items, token);
        }
        catch (OperationCanceledException)
        {
            // cancellazione → ignora
        }

    }

    // =====================================================
    // 🔥 UPLOAD IMMAGINI NELLA CARTELLA FTP
    // =====================================================
    public async Task<string?> ImportImagesToFtpBulkAsync(
        List<ResolvedProduct> products,
        Customer customer,
        CancellationToken token)
    {
        try
        {

            /*
            |--------------------------------------------------------------------------
            | VALIDATION
            |--------------------------------------------------------------------------
            */

            if (
                products == null ||
                !products.Any())
            {
                return null;
            }

            /*
            |--------------------------------------------------------------------------
            | BATCH ID
            |--------------------------------------------------------------------------
            */

            var batchId =
                products.First().BatchId.ToString();

            // Start dashboard progress for image import.

            await _importToMagento.UpdateImportStatusAsync(batchId, totalImagesToInsert: products.Count, insertImagesStatus: OperationsStatus.Running);

            /*
            |--------------------------------------------------------------------------
            | TEMP ROOT
            |--------------------------------------------------------------------------
            */
            var root = _env.ContentRootPath;
            var parent = Directory.GetParent(root)!.FullName;

            var tempRoot = Path.Combine(
                parent,
                "temp-zip"
            );

            if (!Directory.Exists(tempRoot))
            {
                Directory.CreateDirectory(tempRoot);
            }

            /*
            |--------------------------------------------------------------------------
            | ZIP PATH
            |--------------------------------------------------------------------------
            */

            var zipPath =
                Path.Combine(
                    tempRoot,
                    $"{batchId}.zip");

            /*
            |--------------------------------------------------------------------------
            | DELETE OLD ZIP
            |--------------------------------------------------------------------------
            */

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            /*
            |--------------------------------------------------------------------------
            | CREATE ZIP
            |--------------------------------------------------------------------------
            */

            await CreateImagesZipAsync(products, zipPath, token);

            /*
            |--------------------------------------------------------------------------
            | FTP UPLOAD
            |--------------------------------------------------------------------------
            */

            await UploadFtpAsync(
                zipPath,
                customer,
                token,
                "/var/import/images");

            /*
            |--------------------------------------------------------------------------
            | API
            |--------------------------------------------------------------------------
            */

            await _http.PostAsync(
                $"{BaseUrl}/rest/V1/heron/images-local/{batchId}",
                null,
                token);

            /*
            |--------------------------------------------------------------------------
            | CLEANUP
            |--------------------------------------------------------------------------
            */

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            return batchId;
        }
        catch(Exception e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }

    // =====================================================
    // 🔥 CREA FILE ZIP
    // =====================================================

    public async Task<List<object>> CreateImagesZipAsync(
        IEnumerable<ResolvedProduct> products,
        string zipPath,
        CancellationToken token = default)
    {
        var payload =
            new ConcurrentDictionary<string, List<string>>();

        /*
        |--------------------------------------------------------------------------
        | TEMP ROOT
        |--------------------------------------------------------------------------
        */
        var rootTemp = @"C:\TempZip";

        if (!Directory.Exists(rootTemp))
            Directory.CreateDirectory(rootTemp);

        /*
        |--------------------------------------------------------------------------
        | UNIQUE TEMP FOLDER
        |--------------------------------------------------------------------------
        */
        var tempFolder =
            Path.Combine(
                rootTemp,
                $"zip_{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempFolder);

        try
        {
            /*
            |--------------------------------------------------------------------------
            | FLATTEN
            |--------------------------------------------------------------------------
            */
            var allImages = products
                .Where(p => p.Images != null)
                .SelectMany(p =>
                    p.Images
                     .Where(i => i.GridFsId != null)
                     .Select((img, index) => new
                     {
                         Product = p,
                         Image = img,
                         Index = index + 1
                     }))
                .ToList();

            DebugZip(
                $"START GRIDFS -> TOTAL:{allImages.Count}");

            /*
            |--------------------------------------------------------------------------
            | GRIDFS DOWNLOAD PARALLEL
            |--------------------------------------------------------------------------
            */
            await Parallel.ForEachAsync(
                allImages,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 8,
                    CancellationToken = token
                },
                async (x, ct) =>
                {
                    try
                    {
                        var extension = GetExtension(x.Image.MimeType);
                        var fileName = $"{x.Product.Aic}_{x.Index}{extension}";
                        var filePath = Path.Combine(tempFolder, fileName);

                        await using var fs = new FileStream(
                            filePath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            1024 * 1024,
                            FileOptions.SequentialScan);

                        await _imageStorage.CopyToAsync(
                            x.Image.GridFsId!.Value,
                            fs,
                            ct);
                    }
                    catch (Exception ex)
                    {
                        DebugZip($"GRIDFS ERROR -> {ex.Message}");
                    }
                });

            DebugZip("GRIDFS DOWNLOAD COMPLETED");

            /*
            |--------------------------------------------------------------------------
            | DELETE OLD ZIP
            |--------------------------------------------------------------------------
            */
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            /*
            |--------------------------------------------------------------------------
            | 7-ZIP
            |--------------------------------------------------------------------------
            */
            var sevenZipPath =
                @"C:\Program Files\7-Zip\7z.exe";

            if (!File.Exists(sevenZipPath))
                throw new Exception("7-Zip non trovato");

            var psi = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                WorkingDirectory = tempFolder,
                Arguments =
                    $"a -tzip \"{zipPath}\" * -mx=0 -y",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            DebugZip("START 7ZIP");

            using var process =
                new Process
                {
                    StartInfo = psi
                };

            process.Start();

            var stdOut =
                await process.StandardOutput
                    .ReadToEndAsync();

            var stdErr =
                await process.StandardError
                    .ReadToEndAsync();

            await process.WaitForExitAsync(token);

            DebugZip(
                $"7ZIP EXIT -> {process.ExitCode}");

            if (process.ExitCode != 0)
                throw new Exception(
                    $"7ZIP ERROR -> {stdErr}");

            var finalSize =
                new FileInfo(zipPath)
                    .Length;

            DebugZip(
                $"ZIP DONE -> MB:{Math.Round(finalSize / 1024d / 1024d, 2)}");

            /*
            |--------------------------------------------------------------------------
            | PAYLOAD
            |--------------------------------------------------------------------------
            */
            return payload
                .Select(x => new
                {
                    sku = x.Key,
                    images = x.Value
                })
                .Cast<object>()
                .ToList();
        }
        finally
        {
            /*
            |--------------------------------------------------------------------------
            | CLEANUP
            |--------------------------------------------------------------------------
            */
            try
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(
                        tempFolder,
                        true);

                DebugZip(
                    "TEMP CLEANUP COMPLETED");
            }
            catch
            {
            }
        }
    }
       // =====================================================
      // 🖼 UPLOAD IMMAGINE
      // =====================================================
    public async Task<MagentoInsertResult> UploadImagesAsync(ResolvedProduct p, CancellationToken token)
    {
        var result = new MagentoInsertResult();

        try
        {
            // Cancella immagini esistenti
            await DeleteExistingImagesAsync(p.Aic, token);

            if (p.Images == null || !p.Images.Any())
            {
                result.Success = true;
                return result;
            }

            for (int i = 0; i < p.Images.Count; i++)
            {
                var img = p.Images[i];

                var base64 = await _imageStorage.GetBase64Async(
                    (MongoDB.Bson.ObjectId)img.GridFsId!
                );

                var payload = new
                {
                    entry = new
                    {
                        media_type = "image",
                        label = p.Name,
                        position = i,
                        disabled = false,
                        types = i == 0
                            ? new[] { "image", "small_image", "thumbnail" }
                            : Array.Empty<string>(),
                        content = new
                        {
                            base64_encoded_data = base64,
                            type = "image/jpeg",
                            name = img.AltText ?? $"img{i}.jpg"
                        }
                    }
                };

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/rest/V1/products/{p.Aic}/media"
                );

                request.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                await SendAsync(request, token);
            }

            await _exportRepo.SetStatusAsync(p.BatchId.ToString(), p.Aic, ExportStatus.InsertImages);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<string> SaveBase64ImageAsync(string base64, string sku)
    {
        var bytes = Convert.FromBase64String(base64);

        var fileName = $"{sku}.jpg";
        var path = Path.Combine("images-temp", fileName);

        if(!Directory.Exists("images-temp"))
            Directory.CreateDirectory("images-temp");

        await File.WriteAllBytesAsync(path, bytes);

        return path;
    }
    public async Task UploadImageToMagentoAsync(string batchId, string localPath, string fileName)
    {

        var c = await _customerRepo.GetByIdAsync(batchId);
        if (c == null)
            return;


        using var client = new SftpClient(
            c.Magento!.FtpHost,
            c.Magento!.FtpUser,
            c.Magento!.FtpPassword);

        client.Connect();

        using var fileStream = File.OpenRead(localPath);

        var remotePath = $"" + c.Magento.MagentoRootPath + "/pub/media/import/{fileName}";

        client.UploadFile(fileStream, remotePath, true);

        client.Disconnect();
    }

    public async Task ImportImagesBulkAsync(List<ResolvedProduct> p, CancellationToken token)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 10,
            CancellationToken = token
        };

        await Parallel.ForEachAsync(p.Where(a => a.Images.Count() > 0), options, async (prod, ct) =>
        {
            try
            {
                var img = prod.Images.FirstOrDefault();
                // Cancella immagini esistenti
                await DeleteExistingImagesAsync(prod.Aic!, token);

                var fileName = $"{prod.Aic}.jpg";

                var base64 = await _imageStorage.GetBase64Async(
                        (MongoDB.Bson.ObjectId)img!.GridFsId!
                    );

                var path = await SaveBase64ImageAsync(base64, prod.Aic!);

                await UploadImageToMagentoAsync(prod.BatchId.ToString(), path, fileName);

                await AssignImageToProductAsync(prod.Aic, fileName, ct);

                await _exportRepo.SetStatusAsync(prod.BatchId.ToString(), prod.Aic, ExportStatus.InsertImages);

            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                await _exportRepo.SetStatusAsync(prod.BatchId.ToString(), prod.Aic, ExportStatus.Error);
            }

        });
    }

    public async Task AssignImageToProductAsync(string sku, string fileName, CancellationToken token)
    {
        var payload = new
        {
            entry = new
            {
                media_type = "image",
                label = sku,
                position = 1,
                disabled = false,
                types = new[] { "image", "small_image", "thumbnail" },
                file = $"/import/{fileName}"
            }
        };

        var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"{BaseUrl}/rest/V1/products/{sku}/media"
        );

        req.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        await SendAsync(req, token);
    }

    // =====================================================
    // 🔁 HTTP SAFE SEND
    // =====================================================
    private async Task SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                token);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(token);
                throw new Exception(body);
            }
        }

    // =====================================================
    // 🏗 COSTRUZIONE PRODOTTO
    // =====================================================
    public object BuildMagentoProductWithoutImages(ResolvedProduct p)
    {

        var categoryLinks = new List<object>();

        if (p.MagentoCategoryId != null)
        {
            categoryLinks.Add(new
            {
                position = 0,
                category_id = p.MagentoCategoryId
            });
        }

        var customAttributes = new List<object>
        {
            new { attribute_code = "description", value = p.LongDescription ?? p.ShortDescription },
            new { attribute_code = "short_description", value = p.ShortDescription },
            new { attribute_code = "supplier", value = p.SupplierCode },
            new { attribute_code = "manufacturer", value = p.Producer },
            new { attribute_code = "url_key", value = BuildUrlKey(p.Name, p.Aic) }
        };

        // Add special price when the original list price is greater than the current price.
        if (p.OriginalPrice > p.Price)
        {
            customAttributes.Add(new
            {
                attribute_code = "special_price",
                value = p.Price
            });

            // opzionale
            customAttributes.Add(new
            {
                attribute_code = "special_from_date",
                value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        return new
            {
                sku = p.Aic,
                name = p.Name,
                attribute_set_id = 4,
                price = p.OriginalPrice == 0 ? p.Price : p.OriginalPrice,
                status = 1,
                visibility = 4,
                type_id = "simple",
                weight = 1,
                custom_attributes = customAttributes,
                extension_attributes = new
                {
                    website_ids = new[] { 1 },
                    stock_item = new
                    {
                        qty = p.Availability,
                        is_in_stock = p.Availability > 0
                    },
                    category_links = categoryLinks
                }
            };
    }

    public object BuildMagentoMsiProductWithoutImages(ResolvedProduct p)
    {
        var categoryLinks = new List<object>();

        if (p.MagentoCategoryId != null)
        {
            categoryLinks.Add(new
            {
                position = 0,
                category_id = p.MagentoCategoryId
            });
        }

        return new
        {
            product = new
            {
                sku = p.Aic,
                name = p.Name,
                attribute_set_id = 4,
                price = p.Price,
                status = 1,
                visibility = 4,
                type_id = "simple",
                weight = 1,

                custom_attributes = new[]
                {
                new { attribute_code = "description", value = p.LongDescription ?? p.ShortDescription },
                new { attribute_code = "short_description", value = p.ShortDescription },
                new { attribute_code = "supplier", value = p.SupplierCode },
                new { attribute_code = "manufacturer", value = p.Producer },
                new { attribute_code = "url_key", value = BuildUrlKey(p.Name!, p.Aic!) }
            },

                extension_attributes = new
                {
                    website_ids = new[] { 1 }
                },

                category_links = categoryLinks
            }
        };
    }

    private static string? BuildUrlKey(string name, string sku)
    {
        var slug = name
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "")
            .Replace("/", "-");

        return $"{sku}-{slug}";
    }

    // =====================================================
    // Reads and normalizes Magento attribute options.
    // =====================================================
    public async Task<Dictionary<string, int>> GetAttributeOptionsAsync(string attributeCode, CancellationToken token)
    {
        var url = $"{BaseUrl}/rest/V1/products/attributes/{attributeCode}/options";

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var response = await _http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(body);

        var options = System.Text.Json.JsonSerializer.Deserialize<List<MagentoAttributeOption>>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return options!
            .Where(x => !string.IsNullOrEmpty(x.value))
            .GroupBy(x => x.label.Trim())
            .ToDictionary(
                g => g.Key,
                g => int.Parse(g.First().value)
            );
    }

    public async Task<List<MagentoAttributeOption>> GetAttributeManufacturerAsync(CancellationToken token)
    {
        var url = $"{BaseUrl}/rest/V1/products/attributes/manufacturer/options";

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var response = await _http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(body);

        var options = System.Text.Json.JsonSerializer.Deserialize<List<MagentoAttributeOption>>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return options!;
    }

    // =====================================================
    // Reads the Magento category tree and flattens it into a path-to-id map.
    // =====================================================
    public async Task<Dictionary<string, int>> GetCategoryMapAsync(CancellationToken token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{BaseUrl}/rest/V1/categories"
        );

        var response = await _http.SendAsync(request, token);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(json);

        var root = System.Text.Json.JsonSerializer.Deserialize<MagentoCategory>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var map = new Dictionary<string, int>();

        if (root != null)
            FlattenCategories(root, map, "");

        return map;
    }

    public async Task<List<CategoryNode>> GetCategoryAsync(CancellationToken token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{BaseUrl}/rest/V1/categories"
        );

        var response = await _http.SendAsync(request, token);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(json);

        // parse json
        using var doc = JsonDocument.Parse(json);

        // prendi children_data
        if (!doc.RootElement.TryGetProperty("children_data", out var children))
            throw new Exception("children_data non trovato");

        // deserializza CORRETTAMENTE
        var nodes = System.Text.Json.JsonSerializer.Deserialize<List<CategoryNode>>(
            children.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        return nodes ?? new List<CategoryNode>();
    }

    // =====================================================
    // Recursively flattens a Magento category node.
    // =====================================================
    private void FlattenCategories(
        MagentoCategory node,
        Dictionary<string, int> map,
        string parentPath)
    {
        var currentPath = string.IsNullOrEmpty(parentPath)
            ? node.name
            : $"{parentPath}/{node.name}";

        map[currentPath] = node.id;

        if (node.children_data == null)
            return;

        foreach (var child in node.children_data)
        {
            FlattenCategories(child, map, currentPath);
        }
    }


    public async Task RunMagentoCronAsync(CancellationToken token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_magento.FtpHost))
                throw new Exception("FtpHost non configurato per il customer");

            using var client = new SshClient(
                _magento.FtpHost,
                _magento.FtpUser,
                _magento.FtpPassword
            );

            client.Connect();

            if (!client.IsConnected)
                throw new Exception("Connessione SSH fallita");

            var commandText = $"php {_magento.MagentoRootPath}/bin/magento cron:run";

            // Magento richiede 2 esecuzioni
            var result1 = client.RunCommand(commandText);

            if (!string.IsNullOrWhiteSpace(result1.Error))
                throw new Exception(result1.Error);

            await Task.Delay(20000, token);

            var result2 = client.RunCommand(commandText);

            if (!string.IsNullOrWhiteSpace(result2.Error))
                throw new Exception(result2.Error);

            client.Disconnect();
        }
        catch(Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public async Task<List<MagentoSlimProduct>> GetMagentoProductsSlimAsync(string batchId, CancellationToken token)
    {
        var result = new List<MagentoSlimProduct>();

        int page = 1;
        const int pageSize = 300;
        int total;
        var batch = await _batchRepo.GetByIdAsync(batchId);
        await _batchRepo.UpdateDownloadProducts(batchId, 0, 0);

        try
        {
            do
            {
                token.ThrowIfCancellationRequested();

                var url =
                    $"{BaseUrl}/rest/V1/products?" +
                    $"searchCriteria[currentPage]={page}&" +
                    $"searchCriteria[pageSize]={pageSize}&" +
                    $"fields=items[sku,price,custom_attributes[attribute_code,value],extension_attributes[category_links[category_id]]],total_count";

                var request = new HttpRequestMessage(HttpMethod.Get, url);

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,token);
                var json = await response.Content.ReadAsStringAsync(token);

                if (!response.IsSuccessStatusCode)
                    throw new Exception(json);

                var pageResult = System.Text.Json.JsonSerializer.Deserialize<ProductSearchResult>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (pageResult!.Items == null)
                    break;
                foreach (var item in pageResult.Items)
                {
                    var manufacturer = item.CustomAttributes?
                        .FirstOrDefault(x => x.AttributeCode == "manufacturer")?.Value?.ToString();

                    var supplier = item.CustomAttributes?
                        .FirstOrDefault(x => x.AttributeCode == "supplier")?.Value?.ToString();

                    var description = item.CustomAttributes?
                        .FirstOrDefault(x => x.AttributeCode == "description")?.Value?.ToString();

                    var cat = item!.CustomAttributes!.Where(a => a.AttributeCode.Contains("cat")).ToList();

                   var Categories = ExtractCategories(item.ExtensionAttributes!);

                    result.Add(new MagentoSlimProduct
                    {
                        Sku = item.Sku,
                        Price = item.Price,
                        Manufacturer = manufacturer!,
                        Supplier = supplier!,
                        Description = description!,
                        Categories = Categories
                    });
                }

                total = pageResult.TotalCount;
                // Store Magento download progress on the batch.
                await _batchRepo.UpdateDownloadProducts(batchId, total, result.Count);

                page++;
            } 
                while ((page - 1) * pageSize < total);
            }
            catch(Exception e)
            {
                var ec = e;
            }

        return result;
    }

    public async Task DisableProductsAsync(List<string> skus, CancellationToken token)
    {
        if (skus == null || skus.Count == 0)
            return;

        try
        {
            await ProcessChannelAsync(
                skus,
                async (sku, ct) =>
                {
                    await DisableProductAsync(sku, ct);
                },
                token);

        }
        catch (OperationCanceledException)
        {
            // cancellazione → ignora
        };
    }

    private static List<string> ExtractCategories(JsonElement extensionAttributes)
    {
        var result = new List<string>();

        // Se è [] → ignora
        if (extensionAttributes.ValueKind != JsonValueKind.Object)
            return result;

        if (!extensionAttributes.TryGetProperty("category_links", out var links))
            return result;

        if (links.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var link in links.EnumerateArray())
        {
            if (!link.TryGetProperty("category_id", out var categoryId))
                continue;

            switch (categoryId.ValueKind)
            {
                case JsonValueKind.String:
                    result.Add(categoryId.GetString()!);
                    break;

                case JsonValueKind.Number:
                    result.Add(categoryId.GetRawText());
                    break;

                case JsonValueKind.Array:
                    foreach (var item in categoryId.EnumerateArray())
                        result.Add(item.GetString() ?? item.GetRawText());
                    break;
            }
        }

        return result;
    }
    private static List<string> ExtractCategoriesIds(List<CustomAttribute> customAttributes)
    {
        var result = new List<string>();

        foreach (var link in customAttributes)
        {
            if (!link.AttributeCode.Contains("category") && !link.AttributeCode.Contains("categories"))
                continue;
            else
            {
                var json = System.Text.Json.JsonSerializer.Serialize(link);
                using var doc = JsonDocument.Parse(json);

                result = doc.RootElement
                    .GetProperty("value")
                    .EnumerateArray()
                    .Select(x => x.ToString())
                    .ToList();
            }

        }

        return result;
    }

    private async Task DisableProductAsync(string sku, CancellationToken token)
    {
        var payload = new
        {
            product = new
            {
                sku = sku,
                status = 2 // 2 = Disabled
            }
        };

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{BaseUrl}/rest/V1/products/{sku}"
        );

        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _http.SendAsync(request, token);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception(body);
        }
    }

    private async Task DeleteExistingImagesAsync(string sku, CancellationToken token)
    {
        var getRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{BaseUrl}/rest/V1/products/{sku}/media"
        );

        var response = await _http.SendAsync(getRequest, token);

        var content = await response.Content.ReadAsStringAsync();

        var mediaEntries = System.Text.Json.JsonSerializer.Deserialize<List<MagentoMediaEntry>>(content);

        if (mediaEntries == null || !mediaEntries.Any())
            return;

        foreach (var entry in mediaEntries)
        {
            var deleteRequest = new HttpRequestMessage(
                HttpMethod.Delete,
                $"{BaseUrl}/rest/V1/products/{sku}/media/{entry.id}"
            );

            await SendAsync(deleteRequest, token);
        }
    }

    public async Task<MagentoMetadata> GetMagentoMetadataAsync(string batchId, CancellationToken token)
    {
        // =====================================================
        // CARICAMENTO METADATI MAGENTO
        // =====================================================
        var manufacturersTask = GetAttributeOptionsAsync("manufacturer", token);
        var suppliersTask = GetAttributeOptionsAsync("supplier", token);
        var categoriesTask = GetCategoryMapAsync(token);
        var magentoProductsTask = GetMagentoProductsSlimAsync(batchId, token);

        await Task.WhenAll(
            manufacturersTask,
            suppliersTask,
            categoriesTask,
            magentoProductsTask
        );

        token.ThrowIfCancellationRequested();

        var manufacturers = manufacturersTask.Result
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().Value,
                StringComparer.OrdinalIgnoreCase
            );

        var suppliers = suppliersTask.Result
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().Value,
                StringComparer.OrdinalIgnoreCase
            );

        var categories = categoriesTask.Result
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().Value,
                StringComparer.OrdinalIgnoreCase
            );

        var magentoProducts = magentoProductsTask.Result;


        return new MagentoMetadata()
        {
            manufacturers = manufacturers,
            suppliers = suppliers,
            categories = categories,
            magentoProducts = magentoProducts
        };
    }

    public int? ResolveCategoryId(
        Dictionary<string, int> categoryMap,
        string categoryName,
        CancellationToken token)
            {
                var match = categoryMap
                    .FirstOrDefault(x =>
                        x.Key.EndsWith("/" + categoryName, StringComparison.OrdinalIgnoreCase)
                    );

                if (match.Equals(default(KeyValuePair<string, int>)))
                {
                    var matchNoCat = categoryMap
                        .FirstOrDefault(x =>
                            x.Key.ToLower().EndsWith("smistare", StringComparison.OrdinalIgnoreCase)
                        );
                    if (matchNoCat.Equals(default(KeyValuePair<string, int>)))
                        return null;
                    return matchNoCat.Value;
                }
                return match.Value;
            }


    public async Task ProcessChannelAsync<T>(
        IEnumerable<T> items,
        Func<T, CancellationToken, Task> action,
        CancellationToken token,
        int workers = 8,
        int capacity = 500)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        var writer = Task.Run(async () =>
        {
            foreach (var item in items)
            {
                await channel.Writer.WriteAsync(item, token);
            }

            channel.Writer.Complete();
        }, token);

        var consumers = Enumerable.Range(0, workers)
            .Select(_ => Task.Run(async () =>
            {
                await foreach (var item in channel.Reader.ReadAllAsync(token))
                {
                    await action(item, token);
                }
            }, token))
            .ToArray();

        await writer;
        await Task.WhenAll(consumers);
    }
    public List<CustomerMagentoCategories> FlattenCategoriesNodes(
    List<CategoryNode> nodes,
    string customerId,
    string parentPath = "Default Category")
    {
        var result = new List<CustomerMagentoCategories>();

        if (nodes == null || !nodes.Any())
            return result;

        var n = System.Text.Json.JsonSerializer.Serialize(nodes);

        foreach (var node in nodes)
        {
            // pulizia nome (IMPORTANTISSIMO)
            var cleanName = CleanCategoryName(node.Name);

            // costruzione path
            var path = string.IsNullOrEmpty(parentPath)
                ? cleanName
                : $"{parentPath}/{cleanName}";

            // Keep only levels useful for product mapping.
            if (node.Level <= 4)
            {
                result.Add(new CustomerMagentoCategories
                {
                    Id = $"{customerId}_{node.Id}",
                    CustomerId = customerId,
                    MagentoCategoryId = node.Id,
                    ParentId = node.ParentId,
                    Name = cleanName,
                    Path = path,
                    Level = node.Level,
                    Position = node.Position,
                    IsActive = node.IsActive,
                    ProductCount = node.ProductCount
                });
            }

            // ricorsione sui figli
            if (node.ChildrenData != null && node.ChildrenData.Any())
            {
                var children = FlattenCategoriesNodes(node.ChildrenData, customerId, path);
                result.AddRange(children);
            }
        }

        return result;
    }

    public string CleanCategoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        return name.Split('|')[0].Trim();
    }

    private static string GetExtension(string? mimeType)
    {
        return mimeType?.ToLower() switch
        {
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };
    }

    public async Task<string?> UploadImageNewAsync(
        ProductImage image,
        string sku,
        string batchId,
        Customer customer,
        CancellationToken token)
    {
        try
        {
            /*
            |--------------------------------------------------------------------------
            | GRIDFS CHECK
            |--------------------------------------------------------------------------
            */

            if (image.GridFsId == null)
            {
                return null;
            }

            /*
            |--------------------------------------------------------------------------
            | EXTENSION
            |--------------------------------------------------------------------------
            */

            var extension =
                GetExtension(image.MimeType);

            /*
            |--------------------------------------------------------------------------
            | FILE NAME
            |--------------------------------------------------------------------------
            */

            var fileName =
                $"{sku}_{batchId}{extension}";

            /*
            |--------------------------------------------------------------------------
            | TEMP DIRECTORY
            |--------------------------------------------------------------------------
            */

            var tempDirectory =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "temp-images");

            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }

            /*
            |--------------------------------------------------------------------------
            | TEMP FILE
            |--------------------------------------------------------------------------
            */

            var tempFile =
                Path.Combine(
                    tempDirectory,
                    fileName);

            /*
            |--------------------------------------------------------------------------
            | DOWNLOAD GRIDFS -> FILE
            |--------------------------------------------------------------------------
            */

            await _imageStorage
                .DownloadToFileAsync(
                    image.GridFsId.Value,
                    tempFile,
                    token);

            /*
            |--------------------------------------------------------------------------
            | FTP UPLOAD
            |--------------------------------------------------------------------------
            */

            await UploadFtpAsync(
                tempFile,
                customer,
                token,
                "/var/import/images");

            /*
            |--------------------------------------------------------------------------
            | DELETE TEMP
            |--------------------------------------------------------------------------
            */

            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            /*
            |--------------------------------------------------------------------------
            | RETURN
            |--------------------------------------------------------------------------
            */

            return fileName;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }

    private void DebugZip(string message)
    {
        File.AppendAllText(
            @"C:\temp\zip-debug.log",
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}");
    }
}
