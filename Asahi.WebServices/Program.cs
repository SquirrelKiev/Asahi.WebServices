using System.Diagnostics;
using System.Reflection;
using Amazon.S3;
using Asahi.WebServices;
using Asahi.WebServices.ThemesProxy;
using Asahi.WebServices.Database;
using Asahi.WebServices.Thumbnails;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;
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

builder.Services.Configure<UrlSigningSettings>(
    builder.Configuration.GetSection("UrlSigning")
);

builder.Services.Configure<StorageSettings>(
    builder.Configuration.GetSection("Storage")
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

builder.Services.AddDbContextFactory<AsahiDbContext>((services, options) =>
{
    options.UseNpgsql(services.GetRequiredService<IConfiguration>().GetConnectionString("Asahi.WebServices"),
        x => x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery).UseNodaTime());
});

builder.Services.AddControllers();

builder.Services.AddHealthChecks().AddDbContextCheck<AsahiDbContext>();

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
    .ReadFrom.Configuration(services.GetRequiredService<IConfiguration>())
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen));

builder.Services.AddSingleton<IClock>(SystemClock.Instance);

builder.Services.AddSingleton<UrlSignatureVerifierService>();

// thumbs
builder.Services.AddSingleton<ThumbnailGenerator>();
builder.Services.AddSingleton<AllowedDomainsService>();

// themes
var storageSettings = builder.Configuration.GetSection("Storage").Get<StorageSettings>();
if (storageSettings == null)
{
    Log.Fatal("Storage settings are not configured");
    await Log.CloseAndFlushAsync();
    return 3;
}
if (storageSettings.StorageType == StorageType.S3)
{
    if (storageSettings.S3StorageSettings == null)
    {
        Log.Fatal("Storage:S3StorageSettings is not configured, despite StorageType being set to S3");
        await Log.CloseAndFlushAsync();
        return 3;
    }
    
    builder.Services.AddAWSService<IAmazonS3>();
    builder.Services.AddSingleton<IStorage, S3Storage>();
}

builder.Services.AddSingleton<ThemeCacheEvictor>();
builder.Services.AddHostedService<ThemeCacheEvictor>(sp => sp.GetRequiredService<ThemeCacheEvictor>());
builder.Services.AddSingleton<ThemesProxyService>();
builder.Services.AddHttpClient(ThemesProxyService.ThemesHttpClientName, x =>
{
    x.BaseAddress = new Uri("https://v.animethemes.moe");
    x.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

await using (var db = app.Services.GetRequiredService<IDbContextFactory<AsahiDbContext>>().CreateDbContext())
{
    await db.Database.MigrateAsync();
}

try
{
    app.Services.GetRequiredService<UrlSignatureVerifierService>();
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "UrlSignatureVerifierService failed to initialize. Make sure the UrlSigningSettings are configured correctly.");
    return 2;
}

try
{
    var processInfo = new ProcessStartInfo("ffmpeg", ["-version"])
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    var ffmpegVersionCheck = Process.Start(processInfo);
    if (ffmpegVersionCheck == null)
    {
        throw new Exception("ffmpeg process never started. (More specifically, Process.Start returned null for whatever reason)");
    }

    await ffmpegVersionCheck.WaitForExitAsync();
    if (ffmpegVersionCheck.ExitCode != 0)
    {
        throw new Exception($"Exited with non-zero exit code. {ffmpegVersionCheck.ExitCode}");
    }
}
catch (Exception e)
{
    app.Logger.LogCritical(e, "ffmpeg dependency check failed. Please make sure ffmpeg is installed and working correctly. (Run ffmpeg -version)");
    return 1;
}

app.UseForwardedHeaders();

app.UseSerilogRequestLogging();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseStaticFiles();

app.UseHostFiltering();

app.MapControllers();

await app.RunAsync();

return 0;