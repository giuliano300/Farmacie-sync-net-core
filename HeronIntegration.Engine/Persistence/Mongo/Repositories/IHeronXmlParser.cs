
using HeronIntegration.Shared.Entities;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public interface IHeronXmlParser
{
    IEnumerable<RawProduct> Parse(
        string filePath,
        string customerCode);
}
