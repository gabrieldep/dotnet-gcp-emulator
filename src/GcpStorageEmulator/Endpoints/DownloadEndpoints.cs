using GcpStorageEmulator.Storage;

namespace GcpStorageEmulator.Endpoints;

/// <summary>
/// Path-style download: <c>GET /{bucket}/{object}</c>. This is the form a V4 signed URL points at.
/// Following fake-gcs-server, the emulator does not verify the <c>X-Goog-*</c> signature query
/// parameters — it simply serves the object if it exists. Registered as a low-precedence catch-all
/// so the literal JSON/upload/download routes always win.
/// </summary>
public static class DownloadEndpoints
{
    public static void MapPathStyleDownloadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/{bucket}/{**objectName}", ["GET", "HEAD"], PathStyleDownload);
    }

    private static IResult PathStyleDownload(string bucket, HttpContext context, IObjectStore store)
    {
        string name = ObjectPathHelper.ExtractPathStyleObjectName(context);
        if (string.IsNullOrEmpty(name))
        {
            return GcsResults.NotFound($"No such object: {bucket}/");
        }

        ObjectRecord? record = store.GetObject(bucket, name);
        if (record is null)
        {
            return GcsResults.NotFound($"No such object: {bucket}/{name}");
        }

        if (HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.ContentType = string.IsNullOrEmpty(record.ContentType)
                ? "application/octet-stream"
                : record.ContentType;
            context.Response.ContentLength = record.Size;
            return Results.Empty;
        }

        return ObjectEndpoints.ServeMedia(bucket, name, store);
    }
}
