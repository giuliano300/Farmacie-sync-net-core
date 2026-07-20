using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

public class SupplierStockRepository : ISupplierStockRepository
{
    private readonly MongoContext _context;
    private readonly MongoCompensationService _compensation;
    private readonly ILogger<SupplierStockRepository> _logger;

    public SupplierStockRepository(
        MongoContext context,
        MongoCompensationService compensation,
        ILogger<SupplierStockRepository> logger)
    {
        _context = context;
        _compensation = compensation;
        _logger = logger;
    }

    public async Task InsertManyAsync(IEnumerable<SupplierStock> items)
    {
        await _context.SupplierStocks.InsertManyAsync(items);
    }

    public async Task<List<SupplierStock>> GetByAicAsync(string aic)
    {
        return await _context.SupplierStocks
            .Find(x => x.Aic == aic)
            .ToListAsync();
    }

    public async Task ReplaceSupplierAsync(
    string supplierCode,
    IEnumerable<SupplierStock> items)
    {
        // Snapshot replacement is logically atomic even on standalone MongoDB: the
        // previous supplier rows are restored if delete or insert fails.
        var normalizedCode = supplierCode.ToUpperInvariant();
        var rows = items.ToList();
        var filter = new BsonDocument("SupplierCode", normalizedCode);
        var backup = await _compensation.CreateBackupAsync("supplier_stock", filter);

        try
        {
            await _context.SupplierStocks.DeleteManyAsync(x => x.SupplierCode == normalizedCode);
            if (rows.Count > 0)
                await _context.SupplierStocks.InsertManyAsync(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Sostituzione supplier_stock fallita per {SupplierCode}; avvio rollback",
                normalizedCode);

            try
            {
                await _compensation.RestoreAsync([backup]);
            }
            catch (Exception rollbackException)
            {
                _logger.LogCritical(
                    rollbackException,
                    "Rollback supplier_stock fallito per {SupplierCode}",
                    normalizedCode);
                throw new AggregateException(ex, rollbackException);
            }

            throw;
        }
        finally
        {
            await _compensation.DropBackupsAsync([backup]);
        }
    }

    public async Task<List<SupplierStock>> GetByAicsAsync(List<string> aics)
    {
        return await _context.SupplierStocks
            .Find(x => aics.Contains(x.Aic))
            .ToListAsync();
    }
}
