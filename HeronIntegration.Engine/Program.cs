using HeronIntegration.Engine.DependencyInjection;
using HeronIntegration.Engine.Workers;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;

// Host dedicato ai processi background: qui devono vivere solo worker e servizi senza HTTP.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Heron Integration Engine";
});

builder.Logging.ClearProviders();

var logDirectory = ResolveLogDirectory(builder.Configuration);
SelfLog.Enable(message =>
{
    try
    {
        File.AppendAllText(Path.Combine(logDirectory, "serilog-selflog.txt"), message);
    }
    catch
    {
        // Serilog self diagnostics must never block application startup.
    }
});

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.Logger(lc => lc
        .Filter.ByExcluding(evt =>
            evt.Properties.TryGetValue("LogArea", out var value) &&
            value.ToString().Trim('"') == "MagentoExporter")
        .WriteTo.File(
            Path.Combine(logDirectory, "application-.txt"),
            rollingInterval: RollingInterval.Day,
            shared: true))
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(evt =>
            evt.Properties.TryGetValue("LogArea", out var value) &&
            value.ToString().Trim('"') == "MagentoExporter")
        .WriteTo.File(
            Path.Combine(logDirectory, "magento-exporter-.txt"),
            rollingInterval: RollingInterval.Day,
            shared: true))
    .CreateLogger();

builder.Logging.AddSerilog(Log.Logger, dispose: true);

// Registra repository, client esterni, processor e singleton condivisi con l'API.
builder.Services.AddHeronIntegrationCore(builder.Configuration);

// Hosted services are intentionally registered only in the Worker host.
// The API can reuse processors/repositories without starting background loops.
// Customer cron creates automatic batches; supplier cron refreshes stocks at midnight.
// The orchestrator executes running batches.
builder.Services.AddHostedService<BatchOrchestratorWorker>();
builder.Services.AddHostedService<CustomerCronBatchWorker>();
builder.Services.AddHostedService<SupplierFileImporterWorker>();
builder.Services.AddHostedService<NightBatchFinalizerService>();

// Avvio long-running: i worker restano attivi fino allo stop del processo/servizio.
var host = builder.Build();
host.Run();

static string ResolveLogDirectory(IConfiguration configuration)
{
    var configuredDirectory =
        Environment.GetEnvironmentVariable("HERON_LOG_DIR") ??
        configuration["HeronLogging:LogDirectory"] ??
        @"C:\inetpub\wwwroot\logs";

    try
    {
        Directory.CreateDirectory(configuredDirectory);
        return configuredDirectory;
    }
    catch
    {
        var fallbackDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(fallbackDirectory);
        return fallbackDirectory;
    }
}
