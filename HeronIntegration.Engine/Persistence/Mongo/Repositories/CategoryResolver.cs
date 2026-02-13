using HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class CategoryResolver : ICategoryResolver
{
    private readonly ICategoryMappingRepository _repo;

    public CategoryResolver(ICategoryMappingRepository repo)
    {
        _repo = repo;
    }

    public async Task<(string category, string subCategory)> ResolveAsync(
        string customerId,
        string sourceCategory,
        string sourceSubCategory)
    {
        var mapping = await _repo.FindAsync(
            customerId,
            sourceCategory,
            sourceSubCategory);

        if (mapping == null)
            return (sourceCategory, sourceSubCategory);

        return (mapping.TargetCategory, mapping.TargetSubCategory);
    }

    public async Task<Dictionary<(string, string), (string, string)>> LoadMappingsAsync(string customerId)
    {
        var mappings = await _repo.GetByCustomerAsync(customerId);

        return mappings!.ToDictionary(
            x => (x.SourceCategory, x.SourceSubCategory),
            x => (x.TargetCategory, x.TargetSubCategory)
        );
    }
}
