using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Generated;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Shared.Entities;
using System.Text;
using System.Xml.Linq;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class FarmadatiProductBaseInfoProvider_TE003 : IProductBaseInfoProvider
{
    private readonly FarmadatiSoapClient _client;

    public FarmadatiProductBaseInfoProvider_TE003(FarmadatiSoapClient client)
    {
        _client = client;
    }

    public async Task<ProductBaseInfo?> GetBaseInfoAsync(string productCode)
    {
        var result = await _client.ExecuteQueryAsync(
            "TE003",
            new[]
            {
                "FDI_0001",
                "FDI_1760",
                "FDI_1761",
                "FDI_1778",
                "FDI_1764",
                "FDI_1765",
                "FDI_1766",
                "FDI_1767",
                "FDI_1768",
                "FDI_1769",
                "FDI_1770",
                "FDI_1771",
                "FDI_1772",
                "FDI_1781"
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

        if (result == null)
            return null;

        if (result.NumRecords == 0)
            return null;

        var doc = XDocument.Parse(result.OutputValue);
        var product = doc.Descendants("Product").FirstOrDefault();
        if (product == null)
            return null;

        return new ProductBaseInfo
        {
            ProductCode = productCode,
            Name = product.Element("FDI_1760")?.Value!,
            ShortDescription = product.Element("FDI_1760")?.Value!
        };
    }
}
