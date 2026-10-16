using System.Text.Json;
using GcpStorageEmulator.Configuration;
using GcpStorageEmulator.Models;
using GcpStorageEmulator.Storage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace GcpStorageEmulator.Endpoints;

/// <summary>
/// GCS upload API. <c>POST /upload/storage/v1/b/{bucket}/o</c> dispatches on <c>uploadType</c>:
/// <c>media</c> (raw bytes), <c>multipart</c> (metadata + bytes) and <c>resumable</c> (session
/// init; the follow-up PUTs are handled in <see cref="ResumableUploadEndpoints"/>).
/// </summary>
public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        foreach (string prefix in RoutePrefixes.Upload)
        {
            app.MapPost($"{prefix}/b/{{bucket}}/o", UploadAsync);
        }
    }

    private static async Task<IResult> UploadAsync(string bucket, HttpContext context, IObjectStore store, IOptions<EmulatorOptions> options)
    {
        if (!store.BucketExists(bucket))
        {
            return GcsResults.NotFound($"Not Found: {bucket}");
        }

        string baseUrl = EmulatorUrls.BaseUrl(context, options.Value);
        string uploadType = context.Request.Query["uploadType"].ToString();

        return uploadType switch
        {
            "media" => MediaUpload(bucket, context, store, baseUrl),
            "multipart" => await MultipartUploadAsync(bucket, context, store, baseUrl),
            "resumable" => await ResumableUploadEndpoints.StartSessionAsync(bucket, context, store, baseUrl),
            "" => GcsResults.BadRequest("required", "Required parameter: uploadType"),
            _ => GcsResults.BadRequest("invalid", $"Unsupported uploadType: {uploadType}"),
        };
    }

    private static IResult MediaUpload(string bucket, HttpContext context, IObjectStore store, string baseUrl)
    {
        string name = context.Request.Query["name"].ToString();
        if (string.IsNullOrEmpty(name))
        {
            return GcsResults.BadRequest("required", "Required parameter: name");
        }

        var writeOptions = new ObjectWriteOptions
        {
            ContentType = NormalizeContentType(context.Request.ContentType),
        };

        EnableSynchronousIo(context);
        ObjectRecord record = store.PutObject(bucket, name, writeOptions, context.Request.Body);
        return GcsResults.Json(GcsMapper.ToObject(record, baseUrl));
    }

    private static async Task<IResult> MultipartUploadAsync(string bucket, HttpContext context, IObjectStore store, string baseUrl)
    {
        if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out MediaTypeHeaderValue? mediaType)
            || mediaType.Boundary.Value is null)
        {
            return GcsResults.BadRequest("invalid", "Expected a multipart/related body with a boundary.");
        }

        string boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value!;
        var reader = new MultipartReader(boundary, context.Request.Body);

        MultipartSection? metadataSection = await reader.ReadNextSectionAsync();
        if (metadataSection is null)
        {
            return GcsResults.BadRequest("invalid", "Missing object metadata part.");
        }

        ObjectMetadataRequest? metadata;
        try
        {
            using var sr = new StreamReader(metadataSection.Body);
            string json = await sr.ReadToEndAsync();
            metadata = JsonSerializer.Deserialize<ObjectMetadataRequest>(json, GcsJson.Options);
        }
        catch (JsonException)
        {
            return GcsResults.BadRequest("parseError", "Could not parse the object metadata part.");
        }

        MultipartSection? mediaSection = await reader.ReadNextSectionAsync();
        if (mediaSection is null)
        {
            return GcsResults.BadRequest("invalid", "Missing media part.");
        }

        string name = metadata?.Name ?? context.Request.Query["name"].ToString();
        if (string.IsNullOrEmpty(name))
        {
            return GcsResults.BadRequest("required", "Required parameter: name");
        }

        var writeOptions = new ObjectWriteOptions
        {
            ContentType = metadata?.ContentType ?? NormalizeContentType(mediaSection.ContentType),
            Metadata = metadata?.Metadata,
            ContentEncoding = metadata?.ContentEncoding,
            ContentDisposition = metadata?.ContentDisposition,
            ContentLanguage = metadata?.ContentLanguage,
            CacheControl = metadata?.CacheControl,
        };

        EnableSynchronousIo(context);
        ObjectRecord record = store.PutObject(bucket, name, writeOptions, mediaSection.Body);
        return GcsResults.Json(GcsMapper.ToObject(record, baseUrl));
    }

    internal static string? NormalizeContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ? null : contentType;

    /// <summary>
    /// The store reads request bodies synchronously; Kestrel forbids that by default. Enabling
    /// synchronous IO per upload request avoids buffering large bodies into memory. Fine for an
    /// emulator; a production service would stream asynchronously instead.
    /// </summary>
    internal static void EnableSynchronousIo(HttpContext context)
    {
        IHttpBodyControlFeature? feature = context.Features.Get<IHttpBodyControlFeature>();
        if (feature is not null)
        {
            feature.AllowSynchronousIO = true;
        }
    }
}
