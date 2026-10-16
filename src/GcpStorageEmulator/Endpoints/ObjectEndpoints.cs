using GcpStorageEmulator.Configuration;
using GcpStorageEmulator.Models;
using GcpStorageEmulator.Storage;
using Microsoft.Extensions.Options;

namespace GcpStorageEmulator.Endpoints;

/// <summary>GCS JSON API for objects: get metadata and download media (alt=media).</summary>
public static class ObjectEndpoints
{
    public static void MapObjectEndpoints(this IEndpointRouteBuilder app)
    {
        foreach (string prefix in RoutePrefixes.Json)
        {
            app.MapGet($"{prefix}/b/{{bucket}}/o", ListObjects);
            app.MapGet($"{prefix}/b/{{bucket}}/o/{{**objectName}}", GetObject);
            app.MapDelete($"{prefix}/b/{{bucket}}/o/{{**objectName}}", DeleteObject);
        }

        // mediaLink alias; always serves bytes.
        foreach (string prefix in RoutePrefixes.Download)
        {
            app.MapGet($"{prefix}/b/{{bucket}}/o/{{**objectName}}", DownloadObject);
        }
    }

    private static IResult ListObjects(string bucket, HttpContext context, IObjectStore store, IOptions<EmulatorOptions> options)
    {
        if (!store.BucketExists(bucket))
        {
            return GcsResults.NotFound($"Not Found: {bucket}");
        }

        string prefix = context.Request.Query["prefix"].ToString();
        string delimiter = context.Request.Query["delimiter"].ToString();
        string baseUrl = EmulatorUrls.BaseUrl(context, options.Value);

        var items = new List<GcsObject>();
        var prefixes = new SortedSet<string>(StringComparer.Ordinal);

        foreach (ObjectRecord record in store.ListObjects(bucket))
        {
            if (prefix.Length > 0 && !record.Name.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (delimiter.Length > 0)
            {
                string rest = record.Name[prefix.Length..];
                int idx = rest.IndexOf(delimiter, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    // Roll everything up to the first delimiter into a synthetic "directory".
                    prefixes.Add(prefix + rest[..(idx + delimiter.Length)]);
                    continue;
                }
            }

            items.Add(GcsMapper.ToObject(record, baseUrl));
        }

        return GcsResults.Json(new GcsObjectList
        {
            Items = items.Count > 0 ? items : null,
            Prefixes = prefixes.Count > 0 ? prefixes.ToList() : null,
        });
    }

    private static IResult DeleteObject(string bucket, HttpContext context, IObjectStore store)
    {
        if (!store.BucketExists(bucket))
        {
            return GcsResults.NotFound($"Not Found: {bucket}");
        }

        string name = ObjectPathHelper.ExtractObjectName(context, bucket);
        return store.DeleteObject(bucket, name)
            ? Results.StatusCode(StatusCodes.Status204NoContent)
            : GcsResults.NotFound($"No such object: {bucket}/{name}");
    }

    private static IResult GetObject(string bucket, HttpContext context, IObjectStore store, IOptions<EmulatorOptions> options)
    {
        string name = ObjectPathHelper.ExtractObjectName(context, bucket);
        string alt = context.Request.Query["alt"].ToString();

        if (string.Equals(alt, "media", StringComparison.OrdinalIgnoreCase))
        {
            return ServeMedia(bucket, name, store);
        }

        ObjectRecord? record = store.GetObject(bucket, name);
        return record is null
            ? GcsResults.NotFound($"No such object: {bucket}/{name}")
            : GcsResults.Json(GcsMapper.ToObject(record, EmulatorUrls.BaseUrl(context, options.Value)));
    }

    private static IResult DownloadObject(string bucket, HttpContext context, IObjectStore store)
    {
        string name = ObjectPathHelper.ExtractObjectName(context, bucket);
        return ServeMedia(bucket, name, store);
    }

    internal static IResult ServeMedia(string bucket, string name, IObjectStore store)
    {
        ObjectRecord? record = store.GetObject(bucket, name);
        Stream? content = store.OpenRead(bucket, name);
        if (record is null || content is null)
        {
            content?.Dispose();
            return GcsResults.NotFound($"No such object: {bucket}/{name}");
        }

        string contentType = string.IsNullOrEmpty(record.ContentType) ? "application/octet-stream" : record.ContentType;
        return new MediaResult(record, content, contentType);
    }
}

/// <summary>
/// Streams object bytes with GCS-style metadata headers (X-Goog-*) and range support.
/// </summary>
internal sealed class MediaResult(ObjectRecord record, Stream content, string contentType) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        IHeaderDictionary headers = httpContext.Response.Headers;
        headers["X-Goog-Generation"] = record.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture);
        headers["X-Goog-Metageneration"] = record.Metageneration.ToString(System.Globalization.CultureInfo.InvariantCulture);
        headers["X-Goog-Stored-Content-Length"] = record.Size.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var hashParts = new List<string>(2);
        if (!string.IsNullOrEmpty(record.Crc32c))
        {
            hashParts.Add($"crc32c={record.Crc32c}");
        }

        if (!string.IsNullOrEmpty(record.Md5Hash))
        {
            hashParts.Add($"md5={record.Md5Hash}");
        }

        if (hashParts.Count > 0)
        {
            headers["X-Goog-Hash"] = string.Join(",", hashParts);
        }

        if (!string.IsNullOrEmpty(record.ContentEncoding))
        {
            headers.ContentEncoding = record.ContentEncoding;
        }

        if (!string.IsNullOrEmpty(record.CacheControl))
        {
            headers.CacheControl = record.CacheControl;
        }

        IResult file = Results.File(content, contentType, enableRangeProcessing: true, lastModified: record.Updated, entityTag: null);
        await file.ExecuteAsync(httpContext);
    }
}
