
using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Generated;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Shared.Entities;
using System.Xml.Linq;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class FarmadatiImageProvider_TE004 : IProductImageProvider
{
    private readonly FarmadatiSoapClient _client;
    private readonly FarmadatiImageDownloader _imageDownloader;
    public FarmadatiImageProvider_TE004(
        FarmadatiSoapClient client,
        FarmadatiImageDownloader imageDownloader)
    {
        _client = client;
        _imageDownloader = imageDownloader;
    }

    public async Task<IReadOnlyList<ProductImage>> GetImagesAsync(string productCode)
    {
        var result = await _client.ExecuteQueryAsync(
            "TE004",
            new[]
            {
                "FDI_T456", // codice prodotto
                "FDI_T457", // progressivo
                "FDI_T459"  // nome file immagine
            },
            new[]
            {
                new Filter
                {
                    Key = "FDI_T456",
                    Operator = "=",
                    Value = productCode,
                    OrGroup = 0
                }
            },
            page: 1,
            pageSize: 20
        );

        if (result.NumRecords == 0)
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

            images.Add(new ProductImage
            {
                Base64 = download.Value.Base64,
                MimeType = download.Value.MimeType,
                Order = order,
                Type = "gallery",
                AltText = fileName
            });
        }

        images = images
            .OrderBy(i => i.Order)
            .ToList();

        // Prima immagine = principale
        if (images.Count > 0)
            images[0].Type = "main";

        return images;
    }
}
