using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Generated;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using System.Text;
using System.Xml.Linq;

namespace HeronSync.Infrastructure.Farmadati.Providers;

public class FarmadatiLongDescriptionProvider_TE003 : IProductLongDescriptionProvider
{
    private readonly FarmadatiSoapClient _client;

    public FarmadatiLongDescriptionProvider_TE003(FarmadatiSoapClient client)
    {
        _client = client;
    }

    public async Task<string?> GetLongDescriptionAsync(string productCode)
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

        if (result.NumRecords == 0)
            return null;

        var doc = XDocument.Parse(result.OutputValue);
        var product = doc.Descendants("Product").FirstOrDefault();
        if (product == null)
            return null;

        var sb = new StringBuilder();

        void Add(string title, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            sb.AppendLine($"<h3>{title}</h3>");
            sb.AppendLine($"<p>{value}</p>");
        }

        Add("Denominazione", product.Element("FDI_1760")?.Value);
        Add("Principi attivi", product.Element("FDI_1761")?.Value);
        Add("Eccipienti", product.Element("FDI_1778")?.Value);
        Add("Indicazioni terapeutiche", product.Element("FDI_1764")?.Value);
        Add("Posologia", product.Element("FDI_1765")?.Value);
        Add("Controindicazioni", product.Element("FDI_1766")?.Value);
        Add("Avvertenze e precauzioni", product.Element("FDI_1767")?.Value);
        Add("Interazioni", product.Element("FDI_1768")?.Value);
        Add("Gravidanza e allattamento", product.Element("FDI_1769")?.Value);
        Add("Effetti sulla guida e sull'uso di macchinari", product.Element("FDI_1770")?.Value);
        Add("Effetti indesiderati", product.Element("FDI_1771")?.Value);
        Add("Sovradosaggio", product.Element("FDI_1772")?.Value);
        Add("Conservazione", product.Element("FDI_1781")?.Value);

        var html = sb.ToString();
        return string.IsNullOrWhiteSpace(html) ? null : html;
    }
}
