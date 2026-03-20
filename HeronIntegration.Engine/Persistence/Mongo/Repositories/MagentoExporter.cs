using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Bson.IO;
using Renci.SshNet;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

public class MagentoExporter : IMagentoExporter
{
    private readonly HttpClient _http;
    private readonly ImageStorageService _imageStorage;
    private readonly IExportRepository _exportRepo;
    private readonly MagentoConfig _magento;
    private readonly IBatchRepository _batchRepo;
    private readonly ICustomerRepository _customerRepo;
    private string BaseUrl => _magento.BaseUrl.TrimEnd('/');

    private const int MaxParallel = 8;

    public MagentoExporter(
        HttpClient http,
        ImageStorageService imageStorage,
        IExportRepository exportRepo,
        MagentoConfig magento,
        IBatchRepository batchRepo,
        ICustomerRepository customerRepo)
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
    }

    // =====================================================
    // 🔹 EXPORT SINGOLO (ORA USA PUT → UPSERT PIÙ VELOCE)
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

            //INVIO UNO PER UNO
            if (!c.Msi)
                await ProcessChannelAsync(
                 products,
                 async (p, ct) =>
                 {
                     await UpsertProductAsync(p, batchId, ct);
                 },
                 token);
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
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        var j = JsonSerializer.Serialize(payload);

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

                var j = JsonSerializer.Serialize(payload);

                var req = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/rest/async/bulk/V1/products"
                );

                req.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
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
        }
    }


    // =====================================================
    // 🔥 UPDATE STOCK
    // =====================================================
    public async Task UpdateStockBulkAsync(List<InventoryItem> items, CancellationToken token)
    {
        try
        {
            var b = await _batchRepo.GetByIdAsync(items[0].Id);
            if (b == null)
                return;

            var c = await _customerRepo.GetByIdAsync(b.CustomerId);
            if (c == null)
                return;

            //INVIO UNO PER UNO
            if (!c.Msi)
            {
                await ProcessChannelAsync(
                    items,
                    async (item, ct) =>
                    {
                        await UpdateQuantityAsync(item.Id, item.Sku, item.Qty, ct);
                    },
                    token);
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
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                await SendAsync(req, token);

                await _exportRepo.SetStatusBulkAsync(batch.ToList(), ExportStatus.UpdatePrice);

            }
            catch(Exception e)
            {
                await _exportRepo.SetStatusBulkAsync(batch.ToList(), ExportStatus.Error);
            }
        }
    }
    private async Task UpdateQuantityAsync(string batchId, string sku, int qty, CancellationToken token)
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
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        //var j = JsonSerializer.Serialize(payload);

        await SendAsync(req, token);

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
                await ProcessChannelAsync(
                items,
                async (item, ct) =>
                {
                    await UploadImagesAsync(item, ct);
                },
                token);
            else
                await ImportImagesBulkAsync(items, token);
        }
        catch (OperationCanceledException)
        {
            // cancellazione → ignora
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
                    JsonSerializer.Serialize(payload),
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
                        (MongoDB.Bson.ObjectId)img.GridFsId!
                    );

                var path = await SaveBase64ImageAsync(base64, prod.Aic!);

                await UploadImageToMagentoAsync(prod.BatchId.ToString(), path, fileName);

                await AssignImageToProductAsync(prod.Aic, fileName, ct);

                await _exportRepo.SetStatusAsync(prod.BatchId.ToString(), prod.Aic, ExportStatus.InsertImages);

            }
            catch(Exception e)
            {
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
            JsonSerializer.Serialize(payload),
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

        if (!string.IsNullOrWhiteSpace(p.SubCategory))
        {
            categoryLinks.Add(new
            {
                position = 0,
                category_id = p.SubCategory
            });
        }
        return new
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
                new { attribute_code = "url_key", value = BuildUrlKey(p.Name, p.Aic) }
            },
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

        if (!string.IsNullOrWhiteSpace(p.SubCategory))
        {
            categoryLinks.Add(new
            {
                position = 0,
                category_id = p.SubCategory
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
                new { attribute_code = "url_key", value = BuildUrlKey(p.Name, p.Aic) }
            },

                extension_attributes = new
                {
                    website_ids = new[] { 1 }
                },

                category_links = categoryLinks
            }
        };
    }

    private static string BuildUrlKey(string name, string sku)
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
    // 🔹 GET ATTRIBUTE OPTIONS
    // =====================================================
    public async Task<Dictionary<string, int>> GetAttributeOptionsAsync(string attributeCode, CancellationToken token)
    {
        var url = $"{BaseUrl}/rest/V1/products/attributes/{attributeCode}/options";

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var response = await _http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(body);

        var options = JsonSerializer.Deserialize<List<MagentoAttributeOption>>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return options!
            .Where(x => !string.IsNullOrEmpty(x.value))
            .GroupBy(x => x.label.Trim())
            .ToDictionary(
                g => g.Key,
                g => int.Parse(g.First().value)
            );
    }

    // =====================================================
    // 🔹 GET CATEGORY MAP (FLATTEN TREE)
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

        var root = JsonSerializer.Deserialize<MagentoCategory>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var map = new Dictionary<string, int>();

        if (root != null)
            FlattenCategories(root, map, "");

        return map;
    }

    // =====================================================
    // 🔹 RECURSIVE FLATTEN
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

                var pageResult = JsonSerializer.Deserialize<ProductSearchResult>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (pageResult.Items == null)
                    break;
                foreach (var item in pageResult.Items)
                {
                    var manufacturer = item.CustomAttributes?
                        .FirstOrDefault(x => x.AttributeCode == "manufacturer")?.Value?.ToString();

                    var supplier = item.CustomAttributes?
                        .FirstOrDefault(x => x.AttributeCode == "supplier")?.Value?.ToString();

                    var description = item.CustomAttributes?
                        .FirstOrDefault(x => x.AttributeCode == "description")?.Value?.ToString();

                   var Categories = ExtractCategories(item.ExtensionAttributes);

                    result.Add(new MagentoSlimProduct
                    {
                        Sku = item.Sku,
                        Price = item.Price,
                        Manufacturer = manufacturer,
                        Supplier = supplier,
                        Description = description,
                        Categories = Categories
                    });
                }

                total = pageResult.TotalCount;
                //AGGIORNAMENTO BATCH
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
            JsonSerializer.Serialize(payload),
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

        var mediaEntries = JsonSerializer.Deserialize<List<MagentoMediaEntry>>(content);

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
                            x.Key.EndsWith("/da smistare", StringComparison.OrdinalIgnoreCase)
                        );
                    if (matchNoCat.Equals(default(KeyValuePair<string, int>)))
                        return null;
                    return matchNoCat.Value;
                }
                return match.Value;
            }


    private async Task ProcessChannelAsync<T>(
        IEnumerable<T> items,
        Func<T, CancellationToken, Task> handler,
        CancellationToken token)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(2000)
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        // PRODUCER
        var producer = Task.Run(async () =>
        {
            try
            {
                foreach (var item in items)
                    await channel.Writer.WriteAsync(item, token);
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, token);

        // WORKERS
        var workers = Enumerable.Range(0, MaxParallel)
            .Select(_ => Task.Run(async () =>
            {
                await foreach (var item in channel.Reader.ReadAllAsync(token))
                {
                    await handler(item, token);
                }
            }, token));

        await Task.WhenAll(workers.Prepend(producer));
    }
}