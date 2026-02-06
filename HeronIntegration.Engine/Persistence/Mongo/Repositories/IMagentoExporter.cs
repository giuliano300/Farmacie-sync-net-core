using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Models;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IMagentoExporter
{
    Task ExportAsync(MagentoProductDto product);
}
