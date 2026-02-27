using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HeronIntegration.Shared.Entities;

public class Customer
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string Code { get; set; } = default!; // farmacia

    public string Name { get; set; } = default!;

    public string MagentoStoreCode { get; set; } = default!;

    public string HeronFolder { get; set; } = default!;

    public bool Active { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 
    
    public MagentoConfig? Magento { get; set; }
}

public class MagentoConfig
{
    public string Token { get; set; } = default!;
    public string BaseUrl { get; set; } = default!;
    public string FtpHost { get; set; } = default!;
    public string FtpUser { get; set; } = default!;
    public string FtpPassword { get; set; } = default!;
    public string FtpImportPath { get; set; } = default!;

    public string MagentoRootPath { get; set; } = default!;

    public int? CronDelayMilliseconds { get; set; }
    public int? WebsiteId { get; set; }
    public int? AttributeSetId { get; set; }
}