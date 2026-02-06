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

builder.Services.AddScoped<IStepProcessor, HeronImportStepProcessor>();

builder.Services.AddScoped<IStepProcessor, FarmadatiEnrichmentStepProcessor>();

builder.Services.AddScoped<IRawProductRepository, RawProductRepository>();

builder.Services.AddScoped<IEnrichedProductRepository, EnrichedProductRepository>();

// BASE INFO
builder.Services.AddScoped<IProductBaseInfoProvider, FarmadatiProductBaseInfoProvider_TE001>();
builder.Services.AddScoped<IProductBaseInfoProvider, FarmadatiProductBaseInfoProvider_TE006>();
builder.Services.AddScoped<IProductBaseInfoProvider, CompositeProductBaseInfoProvider>();

// LONG DESCRIPTION
builder.Services.AddScoped<IProductLongDescriptionProvider, FarmadatiLongDescriptionProvider_TR039>();
builder.Services.AddScoped<IProductLongDescriptionProvider, FarmadatiLongDescriptionProvider_TE008>();
builder.Services.AddScoped<IProductLongDescriptionProvider, CompositeLongDescriptionProvider>();

// IMMAGINI
builder.Services.AddScoped<IProductImageProvider, FarmadatiImageProvider_TE004>();
builder.Services.AddScoped<IProductImageProvider, FarmadatiImageProvider_TE009>();
builder.Services.AddScoped<IProductImageProvider, CompositeProductImageProvider>();

builder.Services.AddScoped<IProductEnrichmentService, ProductEnrichmentService>();

builder.Services.AddScoped<IHeronXmlParser, HeronXmlParser>();

builder.Services.AddScoped<ISupplierStockRepository, SupplierStockRepository>();

builder.Services.AddHostedService<SupplierFileImporterWorker>();

builder.Services.AddHttpClient<IMagentoExporter, MagentoExporter>();
builder.Services.AddScoped<IStepProcessor, MagentoExportStepProcessor>();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();

var host = builder.Build();
host.Run();
