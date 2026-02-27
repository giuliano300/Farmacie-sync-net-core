using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Shared.Entities;

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

        public MagentoExporterFactory(
            IHttpClientFactory httpFactory,
            ImageStorageService imageStorage,
            IExportRepository exportRepo)
        {
            _httpFactory = httpFactory;
            _imageStorage = imageStorage;
            _exportRepo = exportRepo;
        }

        public IMagentoExporter Create(MagentoConfig config)
        {
            var http = _httpFactory.CreateClient();

            return new MagentoExporter(
                http,
                _imageStorage,
                _exportRepo,
                config
            );
        }
    }
}
