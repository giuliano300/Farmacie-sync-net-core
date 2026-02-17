using MongoDB.Bson;

namespace HeronIntegration.Shared.Entities;

public class ProductImage
{
    public string? Url { get; set; }   
    public ObjectId? GridFsId { get; set; }      
    public string? Type { get; set; }
    public string? AltText { get; set; }
    public string? MimeType { get; set; }     // image/jpeg, image/png
}
