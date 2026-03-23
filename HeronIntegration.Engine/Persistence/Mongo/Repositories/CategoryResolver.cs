using HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class CategoryResolver : ICategoryResolver
{
    private readonly ICategoryMappingRepository _repo;

    public CategoryResolver(ICategoryMappingRepository repo)
    {
        _repo = repo;
    }

    public async Task<Dictionary<string, int>> LoadMappingsAsync(string customerId)
    {
        var mappings = await _repo.GetByCustomerAsync(customerId);

        return mappings!.ToDictionary(
            x => x.GestionaleKey,
            x => x.MagentoCategoryId
        );
    }
}
