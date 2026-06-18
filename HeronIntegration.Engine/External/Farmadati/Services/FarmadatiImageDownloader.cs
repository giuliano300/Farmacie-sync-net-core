namespace HeronIntegration.Engine.External.Farmadati.Services;

public class FarmadatiImageDownloader
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _password;
    private readonly ILogger<FarmadatiImageDownloader> _logger;

    public FarmadatiImageDownloader(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FarmadatiImageDownloader> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _endpoint = configuration["Farmadati:ImagesEndpoint"]!;
        _password = configuration["Farmadati:Password"]!;
        _logger = logger;
    }

    public async Task<(string Base64, string MimeType)?> DownloadAsBase64Async(
        string datasetCode,
        string fileName)
    {
        try
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

            var base64 = Convert.ToBase64String(bytes);

            // rimuove eventuali newline (importantissimo)
            base64 = base64.Replace("\r", "").Replace("\n", "");

            return (base64, mime);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Errore download immagine base64 {DatasetCode}/{FileName}",
                datasetCode,
                fileName);
        }
        return null;

    }

    public async Task<(byte[] Bytes, string MimeType)?> DownloadAsync(
    string datasetCode,
    string fileName,
    CancellationToken cancellationToken)
    {
        try
        {
            var url =
                $"{_endpoint}" +
                $"?accesskey={_password}" +
                $"&tipodoc={datasetCode}" +
                $"&nomefile={fileName}";

            var response = await _httpClient.GetAsync(
                url, 
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync();

            if (bytes.Length == 0)
                return null;

            var mime =
                response.Content.Headers.ContentType?.MediaType
                ?? "image/jpeg";

            return (bytes, mime);
        }
        catch(Exception ex)
        {
            _logger.LogError(
               ex,
               "Errore download immagine {DatasetCode}/{FileName}",
               datasetCode,
               fileName);
            return null;
        }
    }
}
