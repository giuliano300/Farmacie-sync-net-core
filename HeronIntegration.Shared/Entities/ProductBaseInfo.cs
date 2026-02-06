namespace HeronIntegration.Shared.Entities;

public class ProductBaseInfo
{
    public string ProductCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string ShortDescription { get; set; } = default!;

    public string? AtcCode { get; set; }
    public string? ProductTypeCode { get; set; }

    public ProductCategory Category { get; set; }
}
