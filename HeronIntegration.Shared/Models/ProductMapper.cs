using HeronIntegration.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HeronIntegration.Shared.Models
{
    public static class ProductMapper
    {
        public static ResolvedProduct ToResolved(EnrichedProduct e)
        {
            return new ResolvedProduct
            {
                BatchId = e.BatchId,
                CustomerId = e.CustomerId,
                Aic = e.Aic,

                Name = e.Name,
                Category = e.Category,
                SubCategory = e.SubCategory,
                Producer = e.Producer,

                ShortDescription = e.ShortDescription,
                LongDescription = e.LongDescription,
                Images = e.Images,

                ResolvedAt = DateTime.UtcNow
            };
        }
    }
}
