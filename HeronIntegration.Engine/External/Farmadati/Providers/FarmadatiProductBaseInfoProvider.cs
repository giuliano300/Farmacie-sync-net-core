using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Generated;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Shared.Entities;
using System.Xml.Linq;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class FarmadatiProductBaseInfoProvider : IProductBaseInfoProvider
{
    private readonly FarmadatiSoapClient _client;

    public FarmadatiProductBaseInfoProvider(FarmadatiSoapClient client)
    {
        _client = client;
    }

    public async Task<ProductBaseInfo?> GetBaseInfoAsync(string productCode)
    {
        var result = await _client.ExecuteQueryAsync(
            "TE002",
            new[]
            {
                "FDI_0001", // codice prodotto
                "FDI_0004"  // nome prodotto
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

        if (result.NumRecords == 0 || string.IsNullOrWhiteSpace(result.OutputValue))
            return null;

        var doc = XDocument.Parse(result.OutputValue);

        var productNode = doc
            .Descendants("Product")
            .FirstOrDefault();

        if (productNode == null)
            return null;

        var code = productNode.Element("FDI_0001")?.Value;
        var name = productNode.Element("FDI_0004")?.Value;

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return null;

        return new ProductBaseInfo
        {
            ProductCode = code,
            Name = name,
            ShortDescription = name // fallback iniziale
        };
    }
}
