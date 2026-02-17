
using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Generated;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Shared.Entities;
using System.Xml.Linq;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class FarmadatiImageProvider_TE009 : IProductImageProvider
{
    private readonly FarmadatiSoapClient _client;
    private readonly ImageStorageService _imageStorage;
    private readonly FarmadatiImageDownloader _imageDownloader;
    public FarmadatiImageProvider_TE009(
        FarmadatiSoapClient client,
        FarmadatiImageDownloader imageDownloader,
        ImageStorageService imageStorage)
    {
        _client = client;
        _imageDownloader = imageDownloader;
        _imageStorage = imageStorage;
    }

    public async Task<IReadOnlyList<ProductImage>> GetImagesAsync(string productCode, string name = "")
    {
        try
        {
            var result = await _client.ExecuteQueryAsync(
                "TE009",
                new[] { "FDI_0001", "FDI_0004" },
                new[]
                {
                    new Filter { Key = "FDI_0001", Operator = "=", Value = productCode, OrGroup = 0 }
                },
                page: 1,
                pageSize: 10
            );

            if (result.NumRecords == 0 || string.IsNullOrWhiteSpace(result.OutputValue))
                return Array.Empty<ProductImage>();

            var doc = XDocument.Parse(result.OutputValue);

            var images = new List<ProductImage>();

            foreach (var p in doc.Descendants("Product"))
            {
                var fileName = p.Element("FDI_T459")?.Value;
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                int.TryParse(p.Element("FDI_T457")?.Value, out var order);

                var download = await _imageDownloader
                    .DownloadAsBase64Async("TE004", fileName);

                if (download == null)
                    continue;

                var bytes = Convert.FromBase64String(download.Value.Base64);

                var gridId = await _imageStorage.SaveAsync(fileName, bytes, download.Value.MimeType);

                images.Add(new ProductImage
                {
                    GridFsId = gridId,
                    MimeType = download.Value.MimeType,
                    Type = "gallery",
                    AltText = fileName
                });
            }

            images = images
                .ToList();

            // Prima immagine = principale
            if (images.Count > 0)
                images[0].Type = "main";

            return images;
        }
        catch (Exception s)
        {

        }
        return null;
    }
}
