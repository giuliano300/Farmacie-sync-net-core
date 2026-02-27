using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using HeronIntegration.Shared.Models;
using MongoDB.Bson.IO;
using Renci.SshNet;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;

public class MagentoExporter : IMagentoExporter
{
    private readonly HttpClient _http;
    private readonly ImageStorageService _imageStorage;
    private readonly IExportRepository _exportRepo;
    private readonly MagentoConfig _magento;
    private string BaseUrl => _magento.BaseUrl.TrimEnd('/');

    private const int MaxParallel = 15;

    public MagentoExporter(
        HttpClient http,
        ImageStorageService imageStorage,
        IExportRepository exportRepo,
        MagentoConfig magento)
    {
        _http = http;
        _imageStorage = imageStorage;

        _http.Timeout = TimeSpan.FromMinutes(10);
        _exportRepo = exportRepo;
        _magento = magento;

    }

    // =====================================================
    // 🔹 EXPORT SINGOLO (ORA USA PUT → UPSERT PIÙ VELOCE)
    // =====================================================
    public async Task<MagentoInsertResult> ExportAsync(ResolvedProduct p)
    {
        var result = new MagentoInsertResult();

        try
        {
            await UpsertProductAsync(p);
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
    public async Task ImportProductsAsync(IEnumerable<ResolvedProduct> products)
    {
        using var semaphore = new SemaphoreSlim(MaxParallel);

        var tasks = products.Select(async p =>
        {
            await semaphore.WaitAsync();
            try
            {
                await UpsertProductAsync(p);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    // =====================================================
    // 🔥 UPSERT PRODOTTO (PUT SEMPRE)
    // =====================================================
    private async Task UpsertProductAsync(ResolvedProduct p)
    {
        var payload = new
        {
            product = BuildMagentoProductWithoutImages(p)
        };

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{BaseUrl}/rest/V1/products/{p.Aic}"
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _magento.Token);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        await SendAsync(request);

        //await UpdateQuantityAsync(p.Aic, p.Availability);
    }

    // =====================================================
    // 🔁 HTTP SAFE SEND
    // =====================================================
    private async Task SendAsync(HttpRequestMessage request)
    {
        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception(body);
        }
    }

    // =====================================================
    // 🖼 UPLOAD IMMAGINI  
    // =====================================================
    public async Task<MagentoInsertResult> UploadImagesAsync(ResolvedProduct p)
    {
        var result = new MagentoInsertResult();

        try
        {
            // Cancella immagini esistenti
            await DeleteExistingImagesAsync(p.Aic);

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

                var j = JsonSerializer.Serialize(payload);

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/rest/V1/products/{p.Aic}/media"
                );

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _magento.Token);

                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                await SendAsync(request);
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

    private async Task UpdateQuantityAsync(string batchId, string sku, int qty)
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

        req.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _magento.Token);

        req.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        //var j = JsonSerializer.Serialize(payload);

        var response = await _http.SendAsync(req);

        await _exportRepo.SetStatusAsync(batchId, sku, ExportStatus.UpdatePrice);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"SKU {sku}: {body}");
        }
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
    public async Task<Dictionary<string, int>> GetAttributeOptionsAsync(string attributeCode)
    {
        var url = $"{BaseUrl}/rest/V1/products/attributes/{attributeCode}/options";

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _magento.Token);

        var response = await _http.SendAsync(request);
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
    public async Task<Dictionary<string, int>> GetCategoryMapAsync()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{BaseUrl}/rest/V1/categories"
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _magento.Token);

        var response = await _http.SendAsync(request);
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


    public async Task RunMagentoCronAsync()
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

            await Task.Delay(20000);

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


    public async Task<List<MagentoSlimProduct>> GetMagentoProductsSlimAsync()
    {
        var result = new List<MagentoSlimProduct>();

        int page = 1;
        const int pageSize = 200;
        int total;

        try
        {
            do
            {
                var url =
                    $"{BaseUrl}/rest/V1/products?" +
                    $"searchCriteria[currentPage]={page}&" +
                    $"searchCriteria[pageSize]={pageSize}&" +
                    $"fields=items[sku,price,custom_attributes[attribute_code,value],extension_attributes[category_links[category_id]]],total_count";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _magento.Token);

                var response = await _http.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception(json);

                var pageResult = JsonSerializer.Deserialize<ProductSearchResult>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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

    public async Task DisableProductsAsync(List<string> skus)
    {
        if (skus == null || skus.Count == 0)
            return;

        using var semaphore = new SemaphoreSlim(MaxParallel);

        var tasks = skus.Select(async sku =>
        {
            await semaphore.WaitAsync();

            try
            {
                await DisableProductAsync(sku);
            }
            catch (Exception ex)
            {
                // puoi loggare qui se vuoi
                Console.WriteLine($"Errore disabilitando SKU {sku}: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
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

    private async Task DisableProductAsync(string sku)
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

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _magento.Token);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception(body);
        }
    }

    public async Task UpdateStockBulkAsync(List<InventoryItem> items)
    {
        using var semaphore = new SemaphoreSlim(MaxParallel);

        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                await UpdateQuantityAsync(item.Id, item.Sku, item.Qty);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    public async Task UpdateImageBulkAsync(List<ResolvedProduct> items)
    {
        using var semaphore = new SemaphoreSlim(MaxParallel);

        var tasks = items.Select(async p =>
        {
            await semaphore.WaitAsync();
            try
            {
                await UploadImagesAsync(p);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task DeleteExistingImagesAsync(string sku)
    {
        var getRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{BaseUrl}/rest/V1/products/{sku}/media"
        );

        getRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _magento.Token);

        var response = await _http.SendAsync(getRequest);

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

            deleteRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _magento.Token);

            await SendAsync(deleteRequest);
        }
    }

    public async Task<MagentoMetadata> GetMagentoMetadataAsync()
    {
        // =====================================================
        // CARICAMENTO METADATI MAGENTO
        // =====================================================
        var manufacturersTask = GetAttributeOptionsAsync("manufacturer");
        var suppliersTask = GetAttributeOptionsAsync("supplier");
        var categoriesTask = GetCategoryMapAsync();
        var magentoProductsTask = GetMagentoProductsSlimAsync();

        await Task.WhenAll(
            manufacturersTask,
            suppliersTask,
            categoriesTask,
            magentoProductsTask
        );

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
        string categoryName)
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



}