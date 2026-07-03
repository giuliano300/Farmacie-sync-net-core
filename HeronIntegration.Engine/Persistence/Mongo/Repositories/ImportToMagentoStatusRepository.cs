using HeronIntegration.Engine.Persistence.Mongo.Documents;
using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using MongoDB.Bson;
using MongoDB.Driver;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories;

public class ImportToMagentoStatusRepository : IImportToMagentoStatusRepository
{
    private readonly MongoContext _context;

    public ImportToMagentoStatusRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task InsertAsync(ImportToMagentoStatus import)
    {
        await _context.ImportToMagentoStatus.InsertOneAsync(import);
    }

    public async Task<ImportToMagentoStatus?> GetByBatchAsync(string batchId)
    => await _context.ImportToMagentoStatus.Find(x => x.BatchId == batchId).FirstOrDefaultAsync();

    public async Task UpdateAsync(ImportToMagentoStatus i)
    => await _context.ImportToMagentoStatus
        .ReplaceOneAsync(x => x.Id == i.Id, i);

    public async Task UpdateImportStatusAsync(
        string batchId,
        int? totalProductsToInsert = null,
        int? totalProductsToUpdate = null,
        int? totalImagesToInsert = null,
        int? totalProductsInserted = null,
        int? totalProductsUpdated = null,
        int? totalImagesInserted = null,
        decimal? reindexPercent = null,
        OperationsStatus? insertProductsStatus = null,
        OperationsStatus? updateProductsStatus = null,
        OperationsStatus? insertImagesStatus = null,
        OperationsStatus? importStatus = null,
        OperationsStatus? reindexStatus = null)
    {
        var filter =
            Builders<ImportToMagentoStatus>
                .Filter
                .Eq(x => x.BatchId, batchId);

        var builder =
            Builders<ImportToMagentoStatus>.Update;

        var updates =
            new List<UpdateDefinition<ImportToMagentoStatus>>();

        // SET
        if (totalProductsToInsert.HasValue)
            updates.Add(
                builder.Set(
                    x => x.TotalProductsToInsert,
                    totalProductsToInsert.Value
                )
            );

        if (totalProductsToUpdate.HasValue)
            updates.Add(
                builder.Set(
                    x => x.TotalProductsToUpdate,
                    totalProductsToUpdate.Value
                )
            );

        if (totalImagesToInsert.HasValue)
            updates.Add(
                builder.Set(
                    x => x.TotalImagesToInsert,
                    totalImagesToInsert.Value
                )
            );

        // INC
        if (totalProductsInserted.HasValue)
            updates.Add(
                builder.Inc(
                    x => x.TotalProductsInserted,
                    totalProductsInserted.Value
                )
            );

        if (totalProductsUpdated.HasValue)
            updates.Add(
                builder.Inc(
                    x => x.TotalProductsUpdated,
                    totalProductsUpdated.Value
                )
            );

        if (totalImagesInserted.HasValue)
            updates.Add(
                builder.Inc(
                    x => x.TotalImagesInserted,
                    totalImagesInserted.Value
                )
            );

        if (reindexPercent.HasValue)
            updates.Add(
                builder.Set(
                    x => x.ReindexPercent,
                    reindexPercent.Value
                )
            );

        // STATUS
        if (insertProductsStatus.HasValue)
            updates.Add(
                builder.Set(
                    x => x.InsertProductsStatus,
                    insertProductsStatus.Value
                )
            );

        if (updateProductsStatus.HasValue)
            updates.Add(
                builder.Set(
                    x => x.UpdateProductsStatus,
                    updateProductsStatus.Value
                )
            );

        if (insertImagesStatus.HasValue)
            updates.Add(
                builder.Set(
                    x => x.InsertImagesStatus,
                    insertImagesStatus.Value
                )
            );

        if (importStatus.HasValue)
            updates.Add(
                builder.Set(
                    x => x.Import,
                    importStatus.Value
                )
            );

        if (reindexStatus.HasValue)
            updates.Add(
                builder.Set(
                    x => x.ReindexStatus,
                    reindexStatus.Value
                )
            );

        if (!updates.Any())
            return;

        await _context.ImportToMagentoStatus.UpdateOneAsync(
            filter,
            builder.Combine(updates)
        );
    }

    public async Task Start(string batchId, int TotalProducts, TypeRun? type = 0)
    {
        var importToMagento = new ImportToMagentoStatus()
        {
            InsertProducts = type == TypeRun.Completo || type == TypeRun.ImportProdotti ? true : false,
            InsertImages = type == TypeRun.Completo || type == TypeRun.ImportImmagini ? true : false,
            UpdateQty = type == TypeRun.Completo || type == TypeRun.UpdatePrezzi ? true : false,
            BatchId = batchId,
            TotalProducts = TotalProducts,
            Import = OperationsStatus.Running
        };

        await _context.ImportToMagentoStatus.InsertOneAsync(importToMagento);
    }



    public async Task DeleteAsync(string Id)
        => await _context.ImportToMagentoStatus
        .DeleteOneAsync(x => x.Id == ObjectId.Parse(Id));


}
