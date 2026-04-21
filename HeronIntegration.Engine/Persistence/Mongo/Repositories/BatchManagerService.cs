using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HeronIntegration.Engine.Persistence.Mongo.Documents;

namespace HeronIntegration.Engine.Persistence.Mongo.Repositories
{
    public class BatchManagerService : IBatchManagerService
    {
        private readonly IBatchRepository _batchRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly IMagentoExporterFactory _magentoFactory;

        public BatchManagerService(
            IBatchRepository batchRepo,
            ICustomerRepository customerRepo,
            IMagentoExporterFactory magentoFactory)
        {
            _batchRepo = batchRepo;
            _customerRepo = customerRepo;
            _magentoFactory = magentoFactory;
        }

        public async Task DeleteAsync(string batchId)
        {
            var batch = await _batchRepo.GetByIdAsync(batchId);

            if (batch != null)
            {
                var customer =
                    await _customerRepo.GetByIdAsync(batch.CustomerId);

                if (customer?.Magento != null)
                {
                    try
                    {
                        var exporter =
                            _magentoFactory.Create(customer.Magento);

                        await exporter.StopMagentoImportAsync(batchId);
                    }
                    catch
                    {
                        // opzionale log errore
                    }
                }
            }

            await _batchRepo.DeleteAsync(batchId);
        }
    }
}