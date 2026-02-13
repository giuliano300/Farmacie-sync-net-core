using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Shared.Entities;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HeronIntegration.Engine.External.Farmadati.Enrichment;

public class FreeImageService : IProductImageProvider
{
    private readonly HttpClient _http;

    private static readonly string[] BlockedDomains =
    {
        "shutterstock",
        "alamy",
        "adobe",
        "instagram",
        "facebook",
        "pinterest",
        "irisfarma",
        "ahaliamed.com",
        "watsons.com"
    };

    public FreeImageService(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
    }

    public async Task<IReadOnlyList<ProductImage>> GetImagesAsync(string aic, string name)
    {
        var request1 = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://duckduckgo.com/?q={Uri.EscapeDataString(name)}&iax=images&ia=images");

        request1.Headers.Add("Accept-Language", "it-IT,it;q=0.9");

        var resp1 = await _http.SendAsync(request1);
        var html = await resp1.Content.ReadAsStringAsync();

        var match = Regex.Match(html, @"vqd=""([^""]+)""");
        if (!match.Success)
            return Array.Empty<ProductImage>();

        var token = match.Groups[1].Value;

        var request2 = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://duckduckgo.com/i.js?l=it-it&o=json&q={Uri.EscapeDataString(name)}&vqd={token}");

        request2.Headers.Add("Referer", "https://duckduckgo.com/");
        request2.Headers.Add("Accept-Language", "it-IT,it;q=0.9");

        var resp2 = await _http.SendAsync(request2);
        if (!resp2.IsSuccessStatusCode)
            return Array.Empty<ProductImage>();

        var json = await resp2.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");

        for (int i = 0; i < results.GetArrayLength(); i++)
        {
            var url = results[i].GetProperty("image").GetString();
            if (string.IsNullOrEmpty(url))
                continue;

            var uri = new Uri(url);

            if (BlockedDomains.Any(d => uri.Host.Contains(d)))
                continue;

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                continue;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0)
                continue;

            var mime = response.Content.Headers.ContentType?.MediaType
                       ?? "image/jpeg";

            return new List<ProductImage>
            {
                new ProductImage
                {
                    Url = url,
                    Base64 = Convert.ToBase64String(bytes),
                    MimeType = mime,
                    Order = 1,
                    Type = "image",
                    AltText = name
                }
            };
        }

        return Array.Empty<ProductImage>();
    }
}
