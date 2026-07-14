using HeronIntegration.Shared.Entities;
using MongoDB.Bson;
using System.Reflection;

namespace HeronIntegration.Shared.Models
{
    public static class ProductMapper
    {
        private const string IdPropertyName = "Id";

        private static void CopyMatchingProperties<TSource, TTarget>(TSource source, TTarget target)
        {
            if (source == null || target == null)
                return;

            var sourceProperties = typeof(TSource)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead && x.Name != IdPropertyName)
                .ToDictionary(x => x.Name);

            var targetProperties = typeof(TTarget)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanWrite && x.Name != IdPropertyName);

            foreach (var targetProperty in targetProperties)
            {
                if (!sourceProperties.TryGetValue(targetProperty.Name, out var sourceProperty))
                    continue;

                if (sourceProperty.PropertyType != targetProperty.PropertyType)
                    continue;

                targetProperty.SetValue(target, sourceProperty.GetValue(source));
            }
        }

        public static EnrichedProduct ToMinimalEnriched(RawProduct raw, string batchId)
        {
            var enriched = new EnrichedProduct
            {
                BatchId = ObjectId.Parse(batchId),
                Name = raw.Name!,
                ShortDescription = raw.Name,
                LongDescription = null,
                Images = new List<ProductImage>(),
                HeronPrice = raw.Price,
                HeronStock = raw.Stock,
                MacroGroup = null,
                CachedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            CopyMatchingProperties(raw, enriched);

            return enriched;
        }

        public static EnrichedProduct ToEnrichedFromCache(
            RawProduct raw,
            FarmadatiCache cache,
            string batchId)
        {
            var enriched = ToMinimalEnriched(raw, batchId);

            enriched.Id = ObjectId.GenerateNewId();
            enriched.Name = cache.Name;
            enriched.ShortDescription = cache.ShortDescription;
            enriched.LongDescription = cache.LongDescription;
            enriched.Images = cache.Images ?? new List<ProductImage>();
            enriched.MacroGroup = cache.MacroGroup;
            enriched.Source = "CACHE";

            return enriched;
        }

        public static ResolvedProduct ToResolved(EnrichedProduct enriched)
        {
            var resolved = new ResolvedProduct
            {
                ResolvedAt = DateTime.UtcNow
            };

            CopyMatchingProperties(enriched, resolved);

            return resolved;
        }

        public static ResolvedProduct ToResolved(
            EnrichedProduct enriched,
            SupplierStock chosen,
            ObjectId batchObjectId)
        {
            var resolved = ToResolved(enriched);

            resolved.Id = ObjectId.GenerateNewId();
            resolved.BatchId = batchObjectId;
            resolved.Price = chosen.Price;
            resolved.Availability = chosen.Availability;
            resolved.SupplierCode = chosen.SupplierCode;

            return resolved;
        }

        public static ResolvedProduct CloneResolved(ResolvedProduct source)
        {
            var target = new ResolvedProduct();

            CopyMatchingProperties(source, target);

            return target;
        }

        public static ResolvedProduct NormalizeForExport(ResolvedProduct source)
        {
            var target = CloneResolved(source);

            target.LongDescription = target.LongDescription?.Trim();
            target.ShortDescription = target.ShortDescription?.Trim();

            return target;
        }

        public static ResolvedProduct NormalizeForMagentoMetadata(
            ResolvedProduct source,
            MagentoMetadata metadata)
        {
            var target = NormalizeForExport(source);

            target.SupplierCode =
                !string.IsNullOrWhiteSpace(source.SupplierCode) &&
                metadata.suppliers!.TryGetValue(source.SupplierCode, out var supplierId)
                    ? supplierId.ToString()
                    : "0";

            target.Producer =
                !string.IsNullOrWhiteSpace(source.Producer) &&
                metadata.manufacturers!.TryGetValue(source.Producer, out var manufacturerId)
                    ? manufacturerId.ToString()
                    : "0";

            return target;
        }
    }
}
