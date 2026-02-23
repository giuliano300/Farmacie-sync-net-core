using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using Renci.SshNet;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class MagentoExporter : IMagentoExporter
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ImageStorageService _imageStorage;

    private const int MaxParallel = 24;

    public MagentoExporter(
        HttpClient http,
        IConfiguration config,
        ImageStorageService imageStorage)
    {
        _http = http;
        _config = config;
        _imageStorage = imageStorage;

        _http.Timeout = TimeSpan.FromMinutes(10);
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
            $"{_config["Magento:BaseUrl"]}/rest/V1/products/{p.Aic}"
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        await SendAsync(request);

        await UpdateQuantityAsync(p.Aic, p.Availability);
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
    // 🖼 UPLOAD IMMAGINI (NON CANCELLA PIÙ TUTTO)
    // =====================================================
    public async Task<MagentoInsertResult> UploadImagesAsync(ResolvedProduct p)
    {
        var result = new MagentoInsertResult();

        try
        {
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
                            type = img.MimeType ?? "image/jpeg",
                            name = img.AltText ?? $"img{i}.jpg"
                        }
                    }
                };

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{_config["Magento:BaseUrl"]}/rest/V1/products/{p.Aic}/media"
                );

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                await SendAsync(request);
            }

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

    private async Task UpdateQuantityAsync(string sku, int qty)
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
            $"{_config["Magento:BaseUrl"]}/rest/V1/products/{sku}/stockItems/1"
        );

        req.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

        req.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        var j = JsonSerializer.Serialize(payload);

        var response = await _http.SendAsync(req);

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
        var url = $"{_config["Magento:BaseUrl"]}/rest/V1/products/attributes/{attributeCode}/options";

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

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
            $"{_config["Magento:BaseUrl"]}/rest/V1/categories"
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

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
        var host = _config["Magento:FtpHost"];
        var user = _config["Magento:FtpUser"];
        var password = _config["Magento:FtpPassword"];

        using var client = new SshClient(host, user, password);

        client.Connect();

        if (!client.IsConnected)
            throw new Exception("Connessione SSH fallita.");

        // Eseguiamo cron 2 volte (Magento lo richiede)
        client.RunCommand("php /var/www/vhosts/upfarma.plumadev.com/httpdocs/bin/magento cron:run");
        await Task.Delay(3000);
        client.RunCommand("php /var/www/vhosts/upfarma.plumadev.com/httpdocs/bin/magento cron:run");

        client.Disconnect();
    }

}