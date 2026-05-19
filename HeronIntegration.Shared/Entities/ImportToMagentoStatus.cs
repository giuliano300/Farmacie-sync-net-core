using HeronIntegration.Shared.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Buffers;

namespace HeronIntegration.Shared.Entities
{
    public class ImportToMagentoStatus
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public string BatchId { get; set; } = default!;

        public bool InsertProducts { get; set; }
        public bool InsertImages { get; set; }
        public bool UpdateQty { get; set; }

        public int TotalProducts { get; set; }
        public int TotalProductsToInsert { get; set; }
        public int TotalProductsToUpdate { get; set; }
        public int TotalImagesToInsert { get; set; }

        public int TotalProductsInserted { get; set; }
        public int TotalProductsUpdated { get; set; }
        public int TotalImagesInserted { get; set; }
        public decimal ReindexPercent { get; set; }

        public OperationsStatus InsertProductsStatus { get; set; }
        public OperationsStatus UpdateProductsStatus { get; set; }
        public OperationsStatus InsertImagesStatus { get; set; }

        public OperationsStatus Import { get; set; }
        public OperationsStatus ReindexStatus { get; set; }

        public ImportToMagentoStatus()
        {
            InsertProducts = false;
            InsertImages = false;
            UpdateQty = false;
            TotalProducts = 0;
            TotalProductsToInsert = 0;
            TotalProductsToUpdate = 0;
            TotalImagesToInsert = 0;

            TotalProductsInserted = 0;
            TotalProductsUpdated = 0;
            TotalImagesInserted = 0;

            ReindexPercent = 0;

            InsertProductsStatus = OperationsStatus.None;
            UpdateProductsStatus = OperationsStatus.None;
            InsertImagesStatus = OperationsStatus.None;
            Import = OperationsStatus.None;
            ReindexStatus = OperationsStatus.None;
        }

    }

}
