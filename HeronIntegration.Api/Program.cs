using HeronIntegration.Engine.DependencyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHeronIntegrationCore(builder.Configuration);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .MinimumLevel.Debug()
    .WriteTo.Logger(lc => lc
        .Filter.ByExcluding(evt =>
            evt.Properties.TryGetValue("ImportType", out var value) &&
            value.ToString().Trim('"') == "Farmadati")
        .WriteTo.File(
            @"C:\inetpub\wwwroot\logs\application-.txt",
            rollingInterval: RollingInterval.Day))
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(evt =>
            evt.Properties.TryGetValue("ImportType", out var value) &&
            value.ToString().Trim('"') == "Farmadati")
        .WriteTo.File(
            @"C:\inetpub\wwwroot\logs\farmadati-import-.txt",
            rollingInterval: RollingInterval.Day))
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
