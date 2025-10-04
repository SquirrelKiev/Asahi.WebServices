using System.Reflection;
using Asahi.WebServices;
using Asahi.WebServices.Controllers;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

// Serilog.Debugging.SelfLog.Enable(Console.Error);

Log.Logger = new LoggerConfiguration().WriteTo
    .Console(
        outputTemplate: "[FALLBACK] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        theme: AnsiConsoleTheme.Sixteen)
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OpenApiSettings>(
    builder.Configuration.GetSection("OpenApi")
);

builder.Services.Configure<AllowedDomainsSettings>(
    builder.Configuration.GetSection("AllowedDomains")
);

var currentAssembly = Assembly.GetExecutingAssembly();
var projectName = currentAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
var informationalVersion = currentAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion;
var repositoryUrl = currentAssembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(x => x.Key == "RepositoryUrl")?.Value;

builder.Services.ConfigureHttpClientDefaults(x =>
{
    x.RemoveAllLoggers().ConfigureHttpClient(client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"{projectName}/{informationalVersion} (+{repositoryUrl})");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
});

builder.Services.AddControllers();

// in .NET 10 this gets support for handling xml docs. just going to wait until then to add proper OpenApi docs
builder.Services.AddOpenApi("v1", openApi =>
{
    openApi.AddDocumentTransformer((document, context, _) =>
    {
        var settings = context.ApplicationServices
            .GetRequiredService<IOptions<OpenApiSettings>>()
            .Value;

        document.Servers = settings.Servers;

        return Task.CompletedTask;
    });
});

builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen));

builder.Services.AddSingleton<ThumbnailGenerator>();
builder.Services.AddSingleton<AllowedDomainsService>();

var app = builder.Build();

app.UseForwardedHeaders();

app.UseSerilogRequestLogging();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseStaticFiles();

app.UseHostFiltering();

app.MapControllers();

await app.RunAsync();