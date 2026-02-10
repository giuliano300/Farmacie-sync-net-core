using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Generated;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using System.Text;
using System.Xml.Linq;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class FarmadatiLongDescriptionProvider_TE010 : IProductLongDescriptionProvider
{
    private readonly FarmadatiSoapClient _client;

    private static readonly Dictionary<string, string> _flagMap = new()
    {
        ["FDI_9060"] = "Dispositivo medico (Dir. 93/42/CEE)",
        ["FDI_9061"] = "Diagnostico in vitro (Dir. 98/79/CEE)",
        ["FDI_9062"] = "DM impiantabile attivo (Dir. 90/385/CEE)",
        ["FDI_9063"] = "Dispositivo medico di uso comune",
        ["FDI_9049"] = "Integratore alimentare notificato",
        ["FDI_9297"] = "Dispositivo medico (Reg. UE 2017/745)",
        ["FDI_9334"] = "IVD (Reg. UE 2017/746)",
        ["FDI_9372"] = "DM classe III impiantabile",
        ["FDI_9373"] = "DM classe 3 / 2b",
        ["FDI_9374"] = "IVD con obbligo UDI",
        ["FDI_9050"] = "Prodotto biologico"
    };

    public FarmadatiLongDescriptionProvider_TE010(FarmadatiSoapClient client)
    {
        _client = client;
    }

    public async Task<string?> GetLongDescriptionAsync(string productCode)
    {
        var fields = _flagMap.Keys.Prepend("FDI_0001").ToArray();

        var result = await _client.ExecuteQueryAsync(
            "TE010",
            fields,
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

        if (string.IsNullOrWhiteSpace(result.OutputValue) || result.OutputValue == "EMPTY")
            return null;

        var doc = XDocument.Parse(result.OutputValue);
        var product = doc.Descendants("Product").FirstOrDefault();
        if (product == null)
            return null;

        var bullets = new List<string>();

        foreach (var kv in _flagMap)
        {
            var value = product.Element(kv.Key)?.Value;

            if (value == "1" || value?.Equals("S", StringComparison.OrdinalIgnoreCase) == true)
                bullets.Add(kv.Value);
        }

        if (bullets.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("<h3>Caratteristiche e conformità</h3>");
        sb.AppendLine("<ul>");

        foreach (var b in bullets)
            sb.AppendLine($"<li>{b}</li>");

        sb.AppendLine("</ul>");

        return sb.ToString();
    }
}
