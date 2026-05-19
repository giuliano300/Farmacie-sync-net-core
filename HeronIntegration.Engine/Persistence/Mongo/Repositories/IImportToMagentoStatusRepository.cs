using HeronIntegration.Shared.Entities;
using HeronIntegration.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public interface IImportToMagentoStatusRepository
    {
        Task InsertAsync(ImportToMagentoStatus import);
        Task Start(string batchId, int TotalProducts, TypeRun? type = 0);
        Task<ImportToMagentoStatus?> GetByBatchAsync(string batchId);
        Task UpdateAsync(ImportToMagentoStatus import);
        Task UpdateImportStatusAsync(
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
            OperationsStatus? reindexStatus = null);
        Task DeleteAsync(string id);
    }
}
