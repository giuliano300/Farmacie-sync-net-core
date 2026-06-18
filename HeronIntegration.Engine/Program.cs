using HeronIntegration.Engine.DependencyInjection;
using HeronIntegration.Engine.Workers;

// Host dedicato ai processi background: qui devono vivere solo worker e servizi senza HTTP.
var builder = Host.CreateApplicationBuilder(args);

// Logging da servizio Windows/console: console e debug bastano per diagnostica locale e deploy base.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Registra repository, client esterni, processor e singleton condivisi con l'API.
builder.Services.AddHeronIntegrationCore(builder.Configuration);

// Hosted services are intentionally registered only in the Worker host.
// The API can reuse processors/repositories without starting background loops.
// Ordine logico:
// 1. crea batch da file Heron;
// 2. orchestra gli step dei batch running;
// 3. aggiorna periodicamente gli stock fornitori.
builder.Services.AddHostedService<BatchOrchestratorWorker>();
builder.Services.AddHostedService<HeronFileWatcherWorker>();
builder.Services.AddHostedService<SupplierFileImporterWorker>();

// Avvio long-running: i worker restano attivi fino allo stop del processo/servizio.
var host = builder.Build();
host.Run();
