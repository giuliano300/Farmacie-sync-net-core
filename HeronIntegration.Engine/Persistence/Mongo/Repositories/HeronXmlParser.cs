using HeronIntegration.Shared.Entities;
using System.Globalization;
using System.Xml.Linq;
namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class HeronXmlParser : IHeronXmlParser
{
    public IEnumerable<RawProduct> Parse(string xmlPath, string customerId)
    {
        var doc = XDocument.Load(xmlPath);

        foreach (var p in doc.Descendants("Prodotto"))
        {
            yield return new RawProduct
            {
                CustomerId = customerId,

                Aic = p.Element("CodiceAIC")?.Value!.Trim(),
                Name = p.Element("Nome")?.Value!.Trim(),

                Category = p.Element("Categoria")?.Value!,
                SubCategory = p.Element("SottoCategoria")?.Value!,

                Price = ParseDecimal(p.Element("PrezzoEShop")?.Value),
                OriginalPrice = ParseDecimal(p.Element("PrezzoIniziale")?.Value),

                Stock = ParseInt(p.Element("Giacenza")?.Value),
                Vat = ParseInt(p.Element("Iva")?.Value),

                AtcGmp = p.Element("ATC_GMP")?.Value,
                Producer = p.Element("Produttore")?.Value!,

                Published = ParseBool(p.Element("Pubblicato")?.Value)
            };
        }
    }

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("it-IT"), out var v)
            ? v
            : 0m;

    private static int ParseInt(string? value)
        => int.TryParse(value, out var v) ? v : 0;

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var v) && v;
}
