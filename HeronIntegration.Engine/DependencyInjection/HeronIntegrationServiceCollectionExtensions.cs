using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.External.Farmadati.FullImportNew;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.StepProcessors;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Engine.Suppliers;
using HeronIntegration.Shared.Singletons;
using HeronSync.Infrastructure.Farmadati.Providers;
using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace HeronIntegration.Engine.DependencyInjection;

public static class HeronIntegrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the application core shared by API and worker hosts.
    /// </summary>
    public static IServiceCollection AddHeronIntegrationCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongo(configuration);
        services.AddRepositories();
        services.AddFarmadatiServices();
        services.AddSupplierServices();
        services.AddStepProcessors();
        services.AddSingleton<BatchProcessManager>();
        services.AddSingleton<FarmadatiJobManager>();

        return services;
    }

    private static IServiceCollection AddMongo(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // MongoClient is thread-safe and intended to be reused for the lifetime of the process.
        var connectionString = configuration["Mongo:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configuration value 'Mongo:ConnectionString' is required.");
        }

        services.AddSingleton<IMongoClient>(_ =>
            new MongoClient(connectionString));

        services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var databaseName = configuration["Mongo:Database"];

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new InvalidOperationException("Configuration value 'Mongo:Database' is required.");
            }

            return client.GetDatabase(databaseName);
        });

        services.AddScoped<MongoContext>();

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Repository are scoped because they depend on MongoContext and are used per API request or worker cycle.
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<ISupplierStockRepository, SupplierStockRepository>();
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<IStepRepository, StepRepository>();
        services.AddScoped<IExportRepository, ExportRepository>();
        services.AddScoped<IRawProductRepository, RawProductRepository>();
        services.AddScoped<IEnrichedProductRepository, EnrichedProductRepository>();
        services.AddScoped<IResolvedProductRepository, ResolvedProductRepository>();
        services.AddScoped<IFarmadatiCacheRepository, FarmadatiCacheRepository>();
        services.AddScoped<IFarmadatiUpdatesRepository, FarmadatiUpdatesRepository>();
        services.AddScoped<ICategoryMappingRepository, CategoryMappingRepository>();
        services.AddScoped<ICategoryResolver, CategoryResolver>();
        services.AddScoped<IProducerMappingRepository, ProducerMappingRepository>();
        services.AddScoped<IProducerResolver, ProducerResolver>();
        services.AddScoped<ICustomerMagentoCategoriesRepository, CustomerMagentoCategoriesRepository>();
        services.AddScoped<ICustomerManagementCategoriesRepository, CustomerManagementCategoriesRepository>();
        services.AddScoped<ICustomerMagentoProducerRepository, CustomerMagentoProducerRepository>();
        services.AddScoped<ICustomerManagementProducerRepository, CustomerManagementProducerRepository>();
        services.AddScoped<IProductToExcludeRepository, ProductToExcludeRepository>();
        services.AddScoped<IManagementCacheRepository, ManagementCacheRepository>();
        services.AddScoped<IBatchReportRepository, BatchReportRepository>();
        services.AddScoped<IAdministratorRepository, AdministratorRepository>();
        services.AddScoped<IBatchFinalizerService, BatchFinalizerService>();
        services.AddScoped<IBatchReportService, BatchReportService>();
        services.AddScoped<ICleanupService, CleanupService>();
        services.AddScoped<IBatchManagerService, BatchManagerService>();
        services.AddScoped<IImportToMagentoStatusRepository, ImportToMagentoStatusRepository>();
        services.AddScoped<CategoryMappingRepository>();
        services.AddScoped<ProducerMappingRepository>();

        return services;
    }

    private static IServiceCollection AddFarmadatiServices(this IServiceCollection services)
    {
        // Farmadati providers are composed by dataset so the enrichment flow can fall back predictably.
        services.AddHttpClient();
        services.AddScoped<FarmadatiSoapClient>();

        services.AddScoped<FarmadatiProductBaseInfoProvider>();
        services.AddScoped<FarmadatiProductBaseInfoProvider_TE001>();
        services.AddScoped<FarmadatiProductBaseInfoProvider_TE003>();
        services.AddScoped<FarmadatiProductBaseInfoProvider_TE006>();
        services.AddScoped<IProductBaseInfoProvider>(sp =>
        {
            // Farmadati exposes product data across several datasets; the composite keeps the fallback order explicit.
            var providers = new IProductBaseInfoProvider[]
            {
                sp.GetRequiredService<FarmadatiProductBaseInfoProvider>(),
                sp.GetRequiredService<FarmadatiProductBaseInfoProvider_TE003>(),
                sp.GetRequiredService<FarmadatiProductBaseInfoProvider_TE001>(),
                sp.GetRequiredService<FarmadatiProductBaseInfoProvider_TE006>(),
            };

            return new CompositeProductBaseInfoProvider(providers);
        });

        services.AddScoped<FarmadatiLongDescriptionProvider_TE003>();
        services.AddScoped<FarmadatiLongDescriptionProvider_TE008>();
        services.AddScoped<FarmadatiLongDescriptionProvider_TE010>();
        services.AddScoped<FarmadatiLongDescriptionProvider_TR039>();
        services.AddScoped<IProductLongDescriptionProvider>(sp =>
        {
            // Long descriptions are queried in priority order across supported Farmadati datasets.
            var providers = new IProductLongDescriptionProvider[]
            {
                sp.GetRequiredService<FarmadatiLongDescriptionProvider_TE003>(),
                sp.GetRequiredService<FarmadatiLongDescriptionProvider_TE008>(),
                sp.GetRequiredService<FarmadatiLongDescriptionProvider_TE010>(),
                sp.GetRequiredService<FarmadatiLongDescriptionProvider_TR039>()
            };

            return new CompositeLongDescriptionProvider(providers);
        });

        services.AddSingleton<ImageStorageService>();
        services.AddHttpClient<FreeImageService>();
        services.AddScoped<FreeImageService>();
        services.AddHttpClient<FarmadatiImageDownloader>();
        services.AddScoped<FarmadatiImageProvider_TE004>();
        services.AddScoped<FarmadatiImageProvider_TE009>();
        services.AddScoped<IProductImageProvider>(sp =>
        {
            // Image lookup first uses paid/official datasets, then falls back to the free image service.
            var providers = new IProductImageProvider[]
            {
                sp.GetRequiredService<FarmadatiImageProvider_TE004>(),
                sp.GetRequiredService<FarmadatiImageProvider_TE009>(),
                sp.GetRequiredService<FreeImageService>()
            };

            return new CompositeProductImageProvider(providers);
        });

        services.AddScoped<IProductEnrichmentService, ProductEnrichmentService>();
        services.AddScoped<IFarmadatiFullImportJob, FarmadatiFullImportJob>();

        return services;
    }

    private static IServiceCollection AddSupplierServices(this IServiceCollection services)
    {
        // Supplier FTP clients and parsers are registered as multi-implementation services.
        // The caller selects the implementation by SupplierCode.
        services.AddScoped<IHeronXmlParser, HeronXmlParser>();
        services.AddScoped<ISupplierStockProcessor, SupplierStockProcessor>();

        services.AddScoped<ISupplierFtpClient, SofarmaFtpClient>();
        services.AddScoped<ISupplierFtpClient, GuacciFtpClient>();
        services.AddScoped<ISupplierFtpClient, AllianceFtpClient>();
        services.AddScoped<ISupplierFtpClient, HeringFtpClient>();

        services.AddScoped<ISupplierParser, SofarmaParser>();
        services.AddScoped<ISupplierParser, GuacciParser>();
        services.AddScoped<ISupplierParser, AllianceParser>();
        services.AddScoped<ISupplierParser, HeringParser>();
        services.AddScoped<ISupplierParser, FarvimaParser>();

        return services;
    }

    private static IServiceCollection AddStepProcessors(this IServiceCollection services)
    {
        // Step processors are resolved by their Step name by BatchOrchestratorWorker and manual API flows.
        services.AddScoped<IStepProcessorResolver, StepProcessorResolver>();
        services.AddScoped<IStepProcessor, HeronImportStepProcessor>();
        services.AddScoped<IStepProcessor, FarmadatiEnrichmentStepProcessor>();
        services.AddScoped<IStepProcessor, SupplierResolutionStepProcessor>();
        services.AddScoped<IStepProcessor, MagentoExportStepProcessor>();
        services.AddScoped<SupplierStockProcessor>();
        services.AddScoped<HeronImportStepProcessor>();
        services.AddScoped<FarmadatiEnrichmentStepProcessor>();
        services.AddScoped<SupplierResolutionStepProcessor>();
        services.AddScoped<MagentoExportStepProcessor>();
        services.AddScoped<IMagentoExporterFactory, MagentoExporterFactory>();
        services.AddHttpClient<IMagentoExporter, MagentoExporter>();

        return services;
    }
}
