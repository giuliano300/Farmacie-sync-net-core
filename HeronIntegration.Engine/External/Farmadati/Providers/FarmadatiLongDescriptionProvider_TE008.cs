using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Generated;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using System.Xml.Linq;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class FarmadatiLongDescriptionProvider_TE008 : IProductLongDescriptionProvider
{
    private readonly FarmadatiSoapClient _client;

    public FarmadatiLongDescriptionProvider_TE008(FarmadatiSoapClient client)
    {
        _client = client;
    }

    public async Task<string?> GetLongDescriptionAsync(string productCode)
    {
        var result = await _client.ExecuteQueryAsync(
            "TE008",
            new[]
            {
                "FDI_0001",
                "FDI_1702"
            },
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

        if (string.IsNullOrWhiteSpace(result.OutputValue))
            return null;

        var doc = XDocument.Parse(result.OutputValue);
        var product = doc.Descendants("Product").FirstOrDefault();
        if (product == null)
            return null;

        var description = product.Element("FDI_1702")?.Value;

        return string.IsNullOrWhiteSpace(description)
            ? null
            : description;
    }
}
