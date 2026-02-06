using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class Supplier
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string FtpHost { get; set; } = default!;

    public string FtpUser { get; set; } = default!;

    public string FtpPassword { get; set; } = default!;

    public string RemoteFile { get; set; } = default!;

    public int Priority { get; set; }

    public bool Active { get; set; } = true;
}
