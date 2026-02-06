using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Generated;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using System.Xml.Linq;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class FarmadatiLongDescriptionProvider_TR039 : IProductLongDescriptionProvider
{
    private readonly FarmadatiSoapClient _client;

    public FarmadatiLongDescriptionProvider_TR039(FarmadatiSoapClient client)
    {
        _client = client;
    }

    public async Task<string?> GetLongDescriptionAsync(string productCode)
    {
        var result = await _client.ExecuteQueryAsync(
            "TR039",
            new[] { "FDI_0001", "FDI_4875" },
            new[]
            {
                new Filter
                {
                    Key = "FDI_0001",
                    Operator = "=",
                    Value = productCode,
                    OrGroup = 0
                }
            },
            page: 1,
            pageSize: 1
        );

        if (string.IsNullOrWhiteSpace(result.OutputValue) || result.OutputValue == "EMPTY")
            return null;

        var doc = XDocument.Parse(result.OutputValue);

        var productNode = doc.Descendants("Product").FirstOrDefault();
        if (productNode == null)
            return null;

        var longDescription = productNode.Element("FDI_4875")?.Value;

        return string.IsNullOrWhiteSpace(longDescription)
            ? null
            : longDescription;
    }
}
