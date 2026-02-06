namespace HeronIntegration.Engine.External.Farmadati.Services;

public class FarmadatiImageDownloader
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _password;

    public FarmadatiImageDownloader(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _endpoint = configuration["Farmadati:ImagesEndpoint"]!;
        _password = configuration["Farmadati:Password"]!;
    }

    public async Task<(string Base64, string MimeType)?> DownloadAsBase64Async(
        string datasetCode,
        string fileName)
    {
        var url =
            $"{_endpoint}" +
            $"?accesskey={_password}" +
            $"&tipodoc={datasetCode}" +
            $"&nomefile={fileName}";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (bytes.Length == 0)
            return null;

        var mime = response.Content.Headers.ContentType?.MediaType
                   ?? "image/jpeg";

        return (Convert.ToBase64String(bytes), mime);
    }
}
