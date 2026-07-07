using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;
using MongoDB.Driver;

namespace HeronIntegration.Engine.Persistence.Mongo
{
    public interface IMagentoExporterFactory
    {
        IMagentoExporter Create(MagentoConfig config);
    }

    public class MagentoExporterFactory : IMagentoExporterFactory
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ImageStorageService _imageStorage;
        private readonly IExportRepository _exportRepo;
        private readonly IBatchRepository _batchRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly IHostEnvironment _env;
        private readonly ICustomerMagentoCategoriesRepository _customerMagentoCategoriesRepository;
        private readonly IImportToMagentoStatusRepository _importToMagentoStatusRepository;
        private readonly IMongoDatabase _database;
        private readonly ILogger<MagentoExporter> _logger;

        public MagentoExporterFactory(
            IHttpClientFactory httpFactory,
            ImageStorageService imageStorage,
            IExportRepository exportRepo,
            IBatchRepository batchRepo,
            ICustomerRepository customerRepo,
            IHostEnvironment env,
            ICustomerMagentoCategoriesRepository customerMagentoCategoriesRepository,
            IImportToMagentoStatusRepository importToMagentoStatusRepository,
            IMongoDatabase database,
            ILogger<MagentoExporter> logger)
        {
            _httpFactory = httpFactory;
            _imageStorage = imageStorage;
            _exportRepo = exportRepo;
            _batchRepo = batchRepo;
            _customerRepo = customerRepo;
            _env = env;
            _customerMagentoCategoriesRepository = customerMagentoCategoriesRepository;
            _importToMagentoStatusRepository = importToMagentoStatusRepository;
            _database = database;
            _logger = logger;
        }

        public IMagentoExporter Create(MagentoConfig config)
        {
            var http = _httpFactory.CreateClient();

            return new MagentoExporter(
                http,
                _imageStorage,
                _exportRepo,
                config,
                _batchRepo,
                _customerRepo,
                _env,
                _customerMagentoCategoriesRepository,
                _importToMagentoStatusRepository,
                _database,
                _logger
            );
        }
    }
}
