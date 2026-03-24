using HeronIntegration.Engine;
using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Engine.External.Farmadati.Services;
using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.StepProcessors;
using HeronIntegration.Engine.Steps;
using HeronIntegration.Engine.Suppliers;
using HeronIntegration.Engine.Workers;
using HeronSync.Infrastructure.Farmadati.Providers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<IBatchRepository, BatchRepository>();

builder.Services.AddHostedService<BatchOrchestratorWorker>();


builder.Services.AddHostedService<HeronFileWatcherWorker>();

builder.Services.AddScoped<IRawProductRepository, RawProductRepository>();

builder.Services.AddScoped<IEnrichedProductRepository, EnrichedProductRepository>();


builder.Services.AddScoped<FarmadatiSoapClient>();

// Farmadati providers
builder.Services.AddScoped<FarmadatiProductBaseInfoProvider>();
builder.Services.AddScoped<FarmadatiProductBaseInfoProvider_TE001>();
builder.Services.AddScoped<FarmadatiProductBaseInfoProvider_TE003>();
builder.Services.AddScoped<FarmadatiProductBaseInfoProvider_TE006>();
builder.Services.AddScoped<IProductBaseInfoProvider>(sp =>
{
    var providers = new IProductBaseInfoProvider[]
    {
        sp.GetRequiredService<FarmadatiProductBaseInfoProvider_TE003>(),
        sp.GetRequiredService<FarmadatiProductBaseInfoProvider>(),
        sp.GetRequiredService<FarmadatiProductBaseInfoProvider_TE001>(),
        sp.GetRequiredService<FarmadatiProductBaseInfoProvider_TE006>()
    };

    return new CompositeProductBaseInfoProvider(providers);
});

builder.Services.AddScoped<FarmadatiLongDescriptionProvider_TE003>();
builder.Services.AddScoped<FarmadatiLongDescriptionProvider_TE008>();
builder.Services.AddScoped<FarmadatiLongDescriptionProvider_TE010>();
builder.Services.AddScoped<FarmadatiLongDescriptionProvider_TR039>();

builder.Services.AddScoped<IProductLongDescriptionProvider>(sp =>
{
    var providers = new IProductLongDescriptionProvider[]
    {
        sp.GetRequiredService<FarmadatiLongDescriptionProvider_TE003>(),
        sp.GetRequiredService<FarmadatiLongDescriptionProvider_TE008>(),
        sp.GetRequiredService<FarmadatiLongDescriptionProvider_TE010>(),
        sp.GetRequiredService<FarmadatiLongDescriptionProvider_TR039>()
    };

    return new CompositeLongDescriptionProvider(providers);
});

builder.Services.AddHttpClient<ImageStorageService>();
builder.Services.AddSingleton<ImageStorageService>();
builder.Services.AddHttpClient<FreeImageService>();
builder.Services.AddScoped<FreeImageService>();
builder.Services.AddScoped<FarmadatiImageProvider_TE004>();
builder.Services.AddScoped<FarmadatiImageProvider_TE009>();
builder.Services.AddScoped<IProductImageProvider>(sp =>
{
    var providers = new IProductImageProvider[]
    {
        sp.GetRequiredService<FarmadatiImageProvider_TE004>(),
        sp.GetRequiredService<FarmadatiImageProvider_TE009>(),
        sp.GetRequiredService<FreeImageService>()
    };

    return new CompositeProductImageProvider(providers);
});

builder.Services.AddScoped<IHeronXmlParser, HeronXmlParser>();

builder.Services.AddScoped<ISupplierStockRepository, SupplierStockRepository>();

builder.Services.AddHostedService<SupplierFileImporterWorker>();

builder.Services.AddScoped<IStepProcessor, HeronImportStepProcessor>();
builder.Services.AddScoped<IStepProcessor, FarmadatiEnrichmentStepProcessor>();
builder.Services.AddScoped<IStepProcessor, SupplierResolutionStepProcessor>();
builder.Services.AddScoped<IStepProcessor, MagentoExportStepProcessor>();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IManagementCacheRepository, ManagementCacheRepository>();

builder.Services.AddScoped<ICategoryMappingRepository, CategoryMappingRepository>();
builder.Services.AddScoped<ICategoryResolver, CategoryResolver>();

builder.Services.AddScoped<IProducerMappingRepository, ProducerMappingRepository>();
builder.Services.AddScoped<IProducerResolver, ProducerResolver>();

builder.Services.AddScoped<ISupplierStockProcessor, SupplierStockProcessor>();

builder.Services.AddScoped<ISupplierFtpClient, SofarmaFtpClient>();
builder.Services.AddScoped<ISupplierFtpClient, GuacciFtpClient>();
builder.Services.AddScoped<ISupplierFtpClient, AllianceFtpClient>();
builder.Services.AddScoped<ISupplierFtpClient, HeringFtpClient>();

builder.Services.AddScoped<ISupplierParser, SofarmaParser>();
builder.Services.AddScoped<ISupplierParser, GuacciParser>();
builder.Services.AddScoped<ISupplierParser, AllianceParser>();
builder.Services.AddScoped<ISupplierParser, HeringParser>();
builder.Services.AddScoped<ISupplierParser, FarvimaParser>();

builder.Services.AddScoped<IManagementCacheRepository, ManagementCacheRepository>();
builder.Services.AddScoped<IBatchReportRepository, BatchReportRepository>();
builder.Services.AddScoped<IAdministratorRepository, AdministratorRepository>();

builder.Services.AddScoped<IBatchFinalizerService, BatchFinalizerService>();
builder.Services.AddScoped<IBatchReportService, BatchReportService>();
builder.Services.AddScoped<ICleanupService, CleanupService>();

builder.Services.AddScoped<CategoryMappingRepository>();
builder.Services.AddScoped<ProducerMappingRepository>();

builder.Services.AddScoped<ICustomerMagentoCategoriesRepository, CustomerMagentoCategoriesRepository>();
builder.Services.AddScoped<ICustomerManagementCategoriesRepository, CustomerManagementCategoriesRepository>();

builder.Services.AddScoped<ICustomerMagentoProducerRepository, CustomerMagentoProducerRepository>();
builder.Services.AddScoped<ICustomerManagementProducerRepository, CustomerManagementProducerRepository>();


builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IMagentoExporterFactory, MagentoExporterFactory>();
builder.Services.AddSingleton<BatchProcessManager>();

builder.Services.AddScoped<IFarmadatiUpdatesRepository, FarmadatiUpdatesRepository>();

var host = builder.Build();
host.Run();
