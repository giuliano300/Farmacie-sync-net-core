
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Shared.Entities;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class CompositeProductImageProvider : IProductImageProvider
{
    private readonly IEnumerable<IProductImageProvider> _providers;

    public CompositeProductImageProvider(IEnumerable<IProductImageProvider> providers)
    {
        _providers = providers;
    }

    public async Task<IReadOnlyList<ProductImage>> GetImagesAsync(string productCode)
    {
        foreach (var provider in _providers)
        {
            var images = await provider.GetImagesAsync(productCode);
            if (images.Any())
                return images;
        }

        return Array.Empty<ProductImage>();
    }
}
