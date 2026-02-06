using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Generated;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Shared.Entities;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class FarmadatiProductBaseInfoProvider_TE006 : IProductBaseInfoProvider
{
    private readonly FarmadatiSoapClient _client;

    public FarmadatiProductBaseInfoProvider_TE006(FarmadatiSoapClient client)
    {
        _client = client;
    }

    public async Task<ProductBaseInfo?> GetBaseInfoAsync(string productCode)
    {
        var result = await _client.ExecuteQueryAsync(
            "TE006",
            new[] { "FDI_0001", "FDI_0004" },
            new[]
            {
                new Filter { Key = "FDI_0001", Operator = "=", Value = productCode, OrGroup = 0 }
            },
            page: 1,
            pageSize: 1
        );

        if (result.NumRecords == 0 || string.IsNullOrWhiteSpace(result.OutputValue))
            return null;

        var parts = result.OutputValue.Split('|');

        return new ProductBaseInfo
        {
            ProductCode = parts[0],
            Name = parts[1],
            ShortDescription = parts[1]
        };
    }
}
