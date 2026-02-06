using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class MagentoExporter : IMagentoExporter
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public MagentoExporter(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task ExportAsync(MagentoProductDto product)
    {
        var json = JsonSerializer.Serialize(product);

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
        response.EnsureSuccessStatusCode();
    }
}
