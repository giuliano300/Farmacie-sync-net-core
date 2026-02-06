using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.External.Farmadati.Interfaces;

public interface IProductImageProvider
{
    /// <summary>
    /// Recupera le immagini del prodotto.
    /// </summary>
    Task<IReadOnlyList<ProductImage>> GetImagesAsync(string productCode);
}
