using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Models;
using System.Net;
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

    public async Task ExportAsync(ResolvedProduct p)
    {
        var payload = new
        {
            product = BuildMagentoProduct(p)
        };

        var json = JsonSerializer.Serialize(payload);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_config["Magento:BaseUrl"]}/rest/V1/products"
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _config["Magento:Token"]
            );

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception(body);
    }


    public async Task ExportBulkAsync(List<ResolvedProduct> products)
    {
        const int chunkSize = 500;

        foreach (var chunk in products.Chunk(chunkSize))
        {
            var payload = chunk
                .Select(p => new
                {
                    product = BuildMagentoProduct(p)
                })
                .ToList();

            var json = JsonSerializer.Serialize(payload);

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_config["Magento:BaseUrl"]}/rest/async/bulk/V1/products"
            );

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _config["Magento:Token"]);

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Magento bulk error: {body}");
        }
    }


    private async Task<object> BuildMagentoProduct(ResolvedProduct p)
    {
        string urlKey = BuildUrlKey(p.Name, p.Aic);

        var categoryLinks  = new[]
        {
            new
            {
                position = 0,
                category_id = p.SubCategory
            }
        };
        var mediaEntries = new List<object>();

        for (int i = 0; i < p.Images.Count; i++)
        {
            var img = p.Images[i];

            var base64 = await _imageStorage.GetBase64Async((MongoDB.Bson.ObjectId)img.GridFsId!);

            mediaEntries.Add(new
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
                    base64_encoded_data = base64,      // NON modificare
                    type = img.MimeType ?? "image/jpeg",
                    name = img.AltText ?? $"img{i}.jpg"
                }
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
            },
            media_gallery_entries = mediaEntries.ToArray()
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
