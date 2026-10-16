using GcpStorageEmulator.Configuration;
using GcpStorageEmulator.Storage;
using Microsoft.Extensions.Options;

namespace GcpStorageEmulator.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/_health", (IObjectStore store, IOptions<EmulatorOptions> options) =>
            Results.Ok(new
            {
                status = "ok",
                storageRoot = (store as FileSystemObjectStore)?.Root ?? options.Value.StorageRoot,
            }));
    }
}
