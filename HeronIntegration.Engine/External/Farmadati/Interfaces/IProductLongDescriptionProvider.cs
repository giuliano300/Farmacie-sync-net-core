namespace HeronIntegration.Engine.External.Farmadati.Interfaces;

public interface IProductLongDescriptionProvider
{
    /// <summary>
    /// Recupera la descrizione estesa del prodotto (può essere null).
    /// </summary>
    Task<string?> GetLongDescriptionAsync(string productCode);
}
