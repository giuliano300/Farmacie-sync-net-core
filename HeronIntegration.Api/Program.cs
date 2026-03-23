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
using HeronSync.Infrastructure.Farmadati.Providers;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Mongo
builder.Services.AddSingleton<IMongoClient>(
    _ => new MongoClient(builder.Configuration["Mongo:ConnectionString"])
);

builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(builder.Configuration["Mongo:Database"]);
});

builder.Services.AddScoped<MongoContext>();

// repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISupplierStockRepository, SupplierStockRepository>();
builder.Services.AddScoped<IBatchRepository, BatchRepository>();
builder.Services.AddScoped<IStepRepository, StepRepository>();
builder.Services.AddScoped<IExportRepository, ExportRepository>();
builder.Services.AddScoped<IRawProductRepository, RawProductRepository>();
builder.Services.AddScoped<IEnrichedProductRepository, EnrichedProductRepository>();
builder.Services.AddScoped<IResolvedProductRepository, ResolvedProductRepository>();
builder.Services.AddScoped<CategoryMappingRepository>();
builder.Services.AddScoped<ProducerMappingRepository>();

// HTTP
builder.Services.AddHttpClient();

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

builder.Services.AddScoped<IProductEnrichmentService, ProductEnrichmentService>();


// parser
builder.Services.AddScoped<IHeronXmlParser, HeronXmlParser>();


// step processors
builder.Services.AddScoped<IStepProcessorResolver, StepProcessorResolver>();
builder.Services.AddScoped<IStepProcessor, HeronImportStepProcessor>();
builder.Services.AddScoped<IStepProcessor, FarmadatiEnrichmentStepProcessor>();
builder.Services.AddScoped<IStepProcessor, SupplierResolutionStepProcessor>();
builder.Services.AddScoped<IStepProcessor, MagentoExportStepProcessor>();

builder.Services.AddHttpClient<FarmadatiImageDownloader>()
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

builder.Services.AddScoped<IFarmadatiCacheRepository, FarmadatiCacheRepository>();

builder.Services.AddScoped<ICategoryMappingRepository, CategoryMappingRepository>();
builder.Services.AddScoped<ICategoryResolver, CategoryResolver>();

builder.Services.AddScoped<IProducerMappingRepository, ProducerMappingRepository>();
builder.Services.AddScoped<IProducerResolver, ProducerResolver>();

builder.Services.AddScoped<ICustomerMagentoCategoriesRepository, CustomerMagentoCategoriesRepository>();
builder.Services.AddScoped<ICustomerManagementCategoriesRepository, CustomerManagementCategoriesRepository>();


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

builder.Services.AddScoped<IMagentoExporterFactory, MagentoExporterFactory>();
builder.Services.AddScoped<SupplierStockProcessor>();
builder.Services.AddScoped<HeronImportStepProcessor>();
builder.Services.AddScoped<FarmadatiEnrichmentStepProcessor>();
builder.Services.AddScoped<SupplierResolutionStepProcessor>();
builder.Services.AddScoped<MagentoExportStepProcessor>();
builder.Services.AddSingleton<BatchProcessManager>();

builder.Services.AddScoped<IFarmadatiUpdatesRepository, FarmadatiUpdatesRepository>();


builder.Services.AddCors(options =>
{
    options.AddPolicy("OpenCors",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

app.MapControllers();

app.UseCors("OpenCors");

app.Run();
