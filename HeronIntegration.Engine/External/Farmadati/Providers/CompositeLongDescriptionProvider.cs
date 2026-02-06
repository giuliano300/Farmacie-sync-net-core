
using HeronIntegration.Engine.External.Farmadati.Interfaces;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class CompositeLongDescriptionProvider : IProductLongDescriptionProvider
{
    private readonly IReadOnlyList<IProductLongDescriptionProvider> _providers;

    public CompositeLongDescriptionProvider(
        IReadOnlyList<IProductLongDescriptionProvider> providers)
    {
        _providers = providers;
    }

    public async Task<string?> GetLongDescriptionAsync(string productCode)
    {
        foreach (var provider in _providers)
        {
            var description = await provider.GetLongDescriptionAsync(productCode);

            if (!string.IsNullOrWhiteSpace(description))
                return description;
        }

        return null;
    }
}
