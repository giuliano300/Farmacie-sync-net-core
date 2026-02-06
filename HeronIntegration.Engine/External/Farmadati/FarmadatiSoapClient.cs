using HeronIntegration.Engine.External.Farmadati.Generated;
using System.ServiceModel;

namespace HeronIntegration.Engine.External.Farmadati;

public class FarmadatiSoapClient
{
    private readonly IConfiguration _config;

    public FarmadatiSoapClient(IConfiguration config)
    {
        _config = config;
    }

    private FarmadatiItaliaWebServicesM1Client CreateClient()
    {
        var binding = new BasicHttpBinding
        {
            MaxReceivedMessageSize = int.MaxValue,
            ReaderQuotas = System.Xml.XmlDictionaryReaderQuotas.Max,
            SendTimeout = TimeSpan.FromSeconds(_config.GetValue<int>("Farmadati:TimeoutSeconds")),
            ReceiveTimeout = TimeSpan.FromSeconds(_config.GetValue<int>("Farmadati:TimeoutSeconds"))
        };

        var endpoint = new EndpointAddress(_config["Farmadati:Endpoint"]);

        return new FarmadatiItaliaWebServicesM1Client(binding, endpoint);
    }

    public async Task<ExecuteQuery_Output> ExecuteQueryAsync(
        string dataset,
        string[] fields,
        Filter[] filters,
        int page = 1,
        int pageSize = 1)
    {
        var client = CreateClient();

        try
        {
            return await client.ExecuteQueryAsync(
                _config["Farmadati:Username"]!,
                _config["Farmadati:Password"]!,
                dataset,
                fields,
                filters,
                Array.Empty<Order>(),
                false,
                false,
                page,
                pageSize
            );
        }
        finally
        {
            await client.CloseAsync();
        }
    }
}
