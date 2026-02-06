namespace HeronIntegration.Shared.Entities;

public class ProductImage
{
    public string? Url { get; set; }          // se usi CDN
    public string? Base64 { get; set; }       // se embed
    public int Order { get; set; }
    public string? Type { get; set; }
    public string? AltText { get; set; }
    public string? MimeType { get; set; }     // image/jpeg, image/png
}
