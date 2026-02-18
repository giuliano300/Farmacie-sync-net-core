using FluentFTP;
using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class MagentoExporter : IMagentoExporter
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ImageStorageService _imageStorage;

    public MagentoExporter(HttpClient http, IConfiguration config, ImageStorageService imageStorage)
    {
        _http = http;
        _config = config;
        _imageStorage = imageStorage;
    }

    public async Task<MagentoInsertResult> ExportAsync(ResolvedProduct p)
    {
        var res = new MagentoInsertResult();

        try
        {
            // 1. CREA PRODOTTO SENZA IMMAGINI
            var payload = new
            {
                product = BuildMagentoProductWithoutImages(p)
            };

            var json = JsonSerializer.Serialize(payload);

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_config["Magento:BaseUrl"]}/rest/V1/products"
            );

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                res.Success = false;
                res.ErrorMessage = body;
                return res;
            }

            res.Success = true;
            return res;
        }
        catch (Exception ex)
        {
            res.Success = false;
            res.ErrorMessage = ex.Message;
            return res;
        }
    }
    public async Task<MagentoInsertResult> UploadImagesAsync(ResolvedProduct p)
    {
        var res = new MagentoInsertResult();
        try
        {
            // 1. recupera immagini esistenti
            var get = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_config["Magento:BaseUrl"]}/rest/V1/products/{p.Aic}/media"
            );

            get.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

            var resp = await _http.SendAsync(get);
            var jsonDel = await resp.Content.ReadAsStringAsync();

            var entries = JsonSerializer.Deserialize<List<MagentoMediaEntry>>(jsonDel);

            // 2. cancella immagini esistenti
            foreach (var e in entries!)
            {
                var del = new HttpRequestMessage(
                    HttpMethod.Delete,
                    $"{_config["Magento:BaseUrl"]}/rest/V1/products/{p.Aic}/media/{e.id}"
                );

                del.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

                await _http.SendAsync(del);
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

                var json = JsonSerializer.Serialize(payload);

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{_config["Magento:BaseUrl"]}/rest/V1/products/{p.Aic}/media"
                );

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    res.Success = false;
                    res.ErrorMessage = body;
                    return res;
                }
            }

            res.Success = true;
            return res;
        }
        catch (Exception ex)
        {
            res.Success = false;
            res.ErrorMessage = ex.Message;
            return res;
        }

    }

    public async Task<string> BuildProductBatchAsync(IEnumerable<ResolvedProduct> products)
    {
        var payload = products.Select(p => new
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
            new { attribute_code = "supplier", value = p.SupplierCode },
            new { attribute_code = "manufacturer", value = p.Producer }
        }
        });

        var fileName = $"products_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        var path = Path.Combine("batch", fileName);

        Directory.CreateDirectory("batch");

        var json = JsonSerializer.Serialize(payload);

        await File.WriteAllTextAsync(path, json);

        return path;
    }

    public async Task TriggerBatchImportAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        var payload = new
        {
            file = fileName,
            entity = "catalog_product"
        };

        var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_config["Magento:BaseUrl"]}/rest/V1/import"
        );

        req.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

        req.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        var res = await _http.SendAsync(req);

        if (!res.IsSuccessStatusCode)
            throw new Exception(await res.Content.ReadAsStringAsync());
    }

    public void UploadBatchToMagento(string localFile)
    {
        var ftpUri = new Uri(_config["Magento:FtpUri"]!);
        var remoteFolder = _config["Magento:FtpImportPath"];

        var user = ftpUri.UserInfo.Split(':')[0];
        var pass = ftpUri.UserInfo.Split(':')[1];

        using var client = new FtpClient(ftpUri.Host, user, pass);

        client.Connect();

        var remoteFile = $"{remoteFolder}/{Path.GetFileName(localFile)}";

        using var stream = File.OpenRead(localFile);

        client.UploadStream(stream, remoteFile, FtpRemoteExists.Overwrite, true);

        client.Disconnect();
    }
    public async Task BulkInventoryAsync(IEnumerable<InventoryItem> items)
    {
        var payload = new
        {
            sourceItems = items.Select(i => new
            {
                source_code = "default",
                sku = i.Sku,
                quantity = i.Qty,
                status = i.Qty > 0 ? 1 : 0
            })
        };

        var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_config["Magento:BaseUrl"]}/rest/V1/inventory/source-items"
        );

        req.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

        req.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        var res = await _http.SendAsync(req);

        if (!res.IsSuccessStatusCode)
            throw new Exception(await res.Content.ReadAsStringAsync());
    }

    private object BuildMagentoProductWithoutImages(ResolvedProduct p)
    {
        string urlKey = BuildUrlKey(p.Name, p.Aic);

        var categoryLinks = new[]
        {
        new
        {
            position = 0,
            category_id = p.SubCategory
        }
    };

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
            new { attribute_code = "supplier", value = p.SupplierCode },
            new { attribute_code = "manufacturer", value = p.Producer },
            new { attribute_code = "url_key", value = urlKey }
        },
            extension_attributes = new
            {
                stock_item = new
                {
                    qty = p.Availability,
                    is_in_stock = p.Availability > 0
                },
                category_links = categoryLinks
            }
        };
    }

    private static string BuildUrlKey(string name, string aic)
    {
        var slug = name
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "")
            .Replace("/", "-");

        return $"{aic}-{slug}";
    }

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

        var options = JsonSerializer.Deserialize<List<MagentoAttributeOption>>(body);

        return options!
            .Where(x => !string.IsNullOrEmpty(x.value))
            .GroupBy(x => x.label)
            .ToDictionary(
                g => g.Key,
                g => int.Parse(g.First().value)
            );
    }

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

        var root = JsonSerializer.Deserialize<MagentoCategory>(json);

        var map = new Dictionary<string, int>();

        FlattenCategories(root, map, "");

        return map;
    }

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

}
