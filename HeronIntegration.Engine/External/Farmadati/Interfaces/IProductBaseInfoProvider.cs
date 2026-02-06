

using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.External.Farmadati.Interfaces;

public interface IProductBaseInfoProvider
{
    /// <summary>
    /// Recupera nome e info base del prodotto.
    /// </summary>
    Task<ProductBaseInfo?> GetBaseInfoAsync(string productCode);
}
