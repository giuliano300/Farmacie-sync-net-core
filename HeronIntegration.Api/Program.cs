using HeronIntegration.Engine.DependencyInjection;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHeronIntegrationCore(builder.Configuration);

var logDirectory = @"C:\inetpub\wwwroot\logs";
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
    .MinimumLevel.Warning()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.Logger(lc => lc
        .Filter.ByExcluding(evt =>
            evt.Properties.TryGetValue("ImportType", out var value) &&
            value.ToString().Trim('"') == "Farmadati")
        .WriteTo.File(
            Path.Combine(logDirectory, "application-.txt"),
            rollingInterval: RollingInterval.Day,
            shared: true))
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(evt =>
            evt.Properties.TryGetValue("ImportType", out var value) &&
            value.ToString().Trim('"') == "Farmadati")
        .WriteTo.File(
            Path.Combine(logDirectory, "farmadati-import-.txt"),
            rollingInterval: RollingInterval.Day,
            shared: true))
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddCors(options =>
{
    options.AddPolicy("OpenCors", policy =>
    {
        // The existing frontend expects an open policy. Restrict this via hosting/network rules before exposing publicly.
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("OpenCors");
app.MapControllers();

app.Run();
