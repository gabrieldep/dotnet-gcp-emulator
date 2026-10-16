using GcpStorageEmulator.Configuration;
using GcpStorageEmulator.Endpoints;
using GcpStorageEmulator.Infrastructure;
using GcpStorageEmulator.Storage;
using Microsoft.Extensions.Options;

namespace GcpStorageEmulator;

/// <summary>
/// Shared composition root for the emulator. Kept separate from <c>Program</c> so that
/// integration tests can boot an identical app on a real Kestrel socket.
/// </summary>
public static class EmulatorApp
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<EmulatorOptions>()
            .BindConfiguration(EmulatorOptions.SectionName);

        builder.Services.AddSingleton<IObjectStore, FileSystemObjectStore>();
    }

    public static void Configure(WebApplication app)
    {
        EmulatorOptions options = app.Services.GetRequiredService<IOptions<EmulatorOptions>>().Value;

        if (options.LogRequests)
        {
            app.UseMiddleware<RequestLoggingMiddleware>();
        }

        // Correctness: decompress gzip request bodies the way the real GCS endpoint does.
        app.UseMiddleware<GzipRequestDecompressionMiddleware>();

        app.MapHealthEndpoints();
        app.MapBucketEndpoints();
        app.MapObjectEndpoints();
        app.MapUploadEndpoints();
        app.MapResumableUploadEndpoints();

        // Low-precedence catch-all; must be mapped last.
        app.MapPathStyleDownloadEndpoints();
    }
}
