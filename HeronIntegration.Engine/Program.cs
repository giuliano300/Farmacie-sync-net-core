using HeronIntegration.Engine;
using HeronIntegration.Engine.External.Farmadati;
using HeronIntegration.Engine.External.Farmadati.Enrichment;
using HeronIntegration.Engine.External.Farmadati.Interfaces;
using HeronIntegration.Engine.Persistence.Mongo;
using HeronIntegration.Engine.Persistence.Mongo.Repositories;
using HeronIntegration.Engine.StepProcessors;
using HeronIntegration.Engine.Steps;
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
builder.Services.AddScoped<FarmadatiProductBaseInfoProvider_TE006>();
builder.Services.AddScoped<IProductBaseInfoProvider>(sp =>
{
    var providers = new IProductBaseInfoProvider[]
    {
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

builder.Services.AddHttpClient<IMagentoExporter, MagentoExporter>();

builder.Services.AddScoped<IStepProcessor, HeronImportStepProcessor>();
builder.Services.AddScoped<IStepProcessor, FarmadatiEnrichmentStepProcessor>();
builder.Services.AddScoped<IStepProcessor, SupplierResolutionStepProcessor>();
builder.Services.AddScoped<IStepProcessor, MagentoExportStepProcessor>();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();

builder.Services.AddScoped<ICategoryMappingRepository, CategoryMappingRepository>();
builder.Services.AddScoped<ICategoryResolver, CategoryResolver>();

builder.Services.AddScoped<IProducerMappingRepository, ProducerMappingRepository>();
builder.Services.AddScoped<IProducerResolver, ProducerResolver>();

var host = builder.Build();
host.Run();
