namespace HeronIntegration.Shared.Models;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public long TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}
