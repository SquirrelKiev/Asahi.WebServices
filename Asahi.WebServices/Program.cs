using Asahi.WebServices.Controllers;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
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

var app = builder.Build();

app.UseForwardedHeaders();

app.UseSerilogRequestLogging();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseStaticFiles();

app.UseHostFiltering();

app.MapControllers();

await app.RunAsync();

/// <summary>
/// OpenAPI customizable settings.
/// </summary>
public class OpenApiSettings
{
    /// <summary>
    /// A list of server endpoints.
    /// </summary>
    public List<OpenApiServer> Servers { get; init; } = [];
}