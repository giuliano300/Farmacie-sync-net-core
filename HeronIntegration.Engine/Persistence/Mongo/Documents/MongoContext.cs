using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using MongoDB.Driver;

namespace HeronIntegration.Engine.Persistence.Mongo;

public class MongoContext
{
    private readonly IMongoDatabase _database;

    public MongoContext(IMongoClient client, IConfiguration config)
    {
        _database = client.GetDatabase(config["Mongo:Database"]);
    }

    public IMongoCollection<BatchExecution> BatchExecutions =>
        _database.GetCollection<BatchExecution>("batch_execution");

    public IMongoCollection<StepExecution> StepExecutions =>
    _database.GetCollection<StepExecution>("step_execution");

    public IMongoCollection<ExportExecution> ExportExecutions =>
    _database.GetCollection<ExportExecution>("export_execution");

    public IMongoCollection<RawProduct> RawProducts =>
    _database.GetCollection<RawProduct>("raw_product");

    public IMongoCollection<EnrichedProduct> EnrichedProducts =>
    _database.GetCollection<EnrichedProduct>("enriched_product");

    public IMongoCollection<SupplierStock> SupplierStocks =>
    _database.GetCollection<SupplierStock>("supplier_stock");

    public IMongoCollection<ResolvedProduct> ResolvedProducts =>
    _database.GetCollection<ResolvedProduct>("resolved_product");

    public IMongoCollection<Customer> Customers =>
    _database.GetCollection<Customer>("customers");

    public IMongoCollection<FarmadatiCache> FarmadatiCaches =>
        _database.GetCollection<FarmadatiCache>("farmadati_cache");

    public IMongoCollection<Supplier> Suppliers =>
        _database.GetCollection<Supplier>("suppliers");

    public IMongoCollection<CategoryMapping> CategoryMappings =>
    _database.GetCollection<CategoryMapping>("category_mappings");

    public IMongoCollection<ProducerMapping> ProducerMappings =>
    _database.GetCollection<ProducerMapping>("producer_mappings");

}
