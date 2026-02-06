
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Shared.Entities;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class CompositeProductBaseInfoProvider : IProductBaseInfoProvider
{
    private readonly IReadOnlyList<IProductBaseInfoProvider> _providers;

    public CompositeProductBaseInfoProvider(
        IReadOnlyList<IProductBaseInfoProvider> providers)
    {
        _providers = providers;
    }

    public async Task<ProductBaseInfo?> GetBaseInfoAsync(string productCode)
    {
        foreach (var provider in _providers)
        {
            var info = await provider.GetBaseInfoAsync(productCode);
            if (info != null)
                return info;
        }

        return null;
    }
}
