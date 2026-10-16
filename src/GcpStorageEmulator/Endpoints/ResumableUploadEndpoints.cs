using System.Globalization;
using GcpStorageEmulator.Configuration;
using GcpStorageEmulator.Models;
using GcpStorageEmulator.Storage;
using Microsoft.Extensions.Options;

namespace GcpStorageEmulator.Endpoints;

/// <summary>
/// GCS resumable upload protocol.
/// <list type="number">
/// <item>Init: <c>POST .../o?uploadType=resumable</c> returns 200 with a <c>Location</c> session URI.</item>
/// <item>Transfer: <c>PUT {session}</c> with <c>Content-Range</c>. Incomplete returns 308 + <c>Range</c>;
/// the final chunk returns 200 with the object resource.</item>
/// </list>
/// </summary>
public static class ResumableUploadEndpoints
{
    /// <summary>HTTP 308 "Resume Incomplete" — not in <see cref="StatusCodes"/>.</summary>
    private const int StatusResumeIncomplete = 308;

    public static void MapResumableUploadEndpoints(this IEndpointRouteBuilder app)
    {
        foreach (string prefix in RoutePrefixes.Upload)
        {
            app.MapPut($"{prefix}/b/{{bucket}}/o", PutChunk);
        }
    }

    internal static async Task<IResult> StartSessionAsync(string bucket, HttpContext context, IObjectStore store, string baseUrl)
    {
        ObjectMetadataRequest? metadata = null;
        if (context.Request.HasJsonContentType())
        {
            try
            {
                metadata = await context.Request.ReadFromJsonAsync<ObjectMetadataRequest>(GcsJson.Options);
            }
            catch
            {
                // A resumable init may legitimately have an empty/boundaryless body.
            }
        }

        string name = metadata?.Name ?? context.Request.Query["name"].ToString();
        if (string.IsNullOrEmpty(name))
        {
            return GcsResults.BadRequest("required", "Required parameter: name");
        }

        string? contentType = metadata?.ContentType
            ?? UploadEndpoints.NormalizeContentType(context.Request.Headers["X-Upload-Content-Type"].ToString());

        var writeOptions = new ObjectWriteOptions
        {
            ContentType = contentType,
            Metadata = metadata?.Metadata,
            ContentEncoding = metadata?.ContentEncoding,
            ContentDisposition = metadata?.ContentDisposition,
            ContentLanguage = metadata?.ContentLanguage,
            CacheControl = metadata?.CacheControl,
        };

        ResumableSession session = store.CreateResumableSession(bucket, name, writeOptions);

        string sessionUri = $"{baseUrl}/upload/storage/v1/b/{Uri.EscapeDataString(bucket)}/o?uploadType=resumable&upload_id={session.Id}";
        context.Response.Headers.Location = sessionUri;
        context.Response.Headers["X-GUploader-UploadID"] = session.Id;
        context.Response.StatusCode = StatusCodes.Status200OK;
        return Results.Empty;
    }

    private static IResult PutChunk(string bucket, HttpContext context, IObjectStore store, IOptions<EmulatorOptions> options)
    {
        string uploadId = context.Request.Query["upload_id"].ToString();
        if (string.IsNullOrEmpty(uploadId))
        {
            return GcsResults.BadRequest("required", "Required parameter: upload_id");
        }

        ResumableSession? session = store.GetResumableSession(uploadId);
        if (session is null)
        {
            return GcsResults.NotFound($"Unknown upload session: {uploadId}");
        }

        ContentRangeInfo range = ParseContentRange(context);

        if (range.HasData)
        {
            UploadEndpoints.EnableSynchronousIo(context);
            session = store.WriteResumableChunk(uploadId, range.Start, context.Request.Body);
        }

        context.Response.Headers["X-GUploader-UploadID"] = uploadId;

        bool complete = range.Total.HasValue && session.BytesReceived >= range.Total.Value;
        if (complete)
        {
            ObjectRecord record = store.CompleteResumableSession(uploadId);
            return GcsResults.Json(GcsMapper.ToObject(record, EmulatorUrls.BaseUrl(context, options.Value)));
        }

        context.Response.StatusCode = StatusResumeIncomplete;
        if (session.BytesReceived > 0)
        {
            context.Response.Headers["Range"] = $"bytes=0-{session.BytesReceived - 1}";
        }

        return Results.Empty;
    }

    private static ContentRangeInfo ParseContentRange(HttpContext context)
    {
        string header = context.Request.Headers["Content-Range"].ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            // No Content-Range at all: the whole body is the object.
            long? length = context.Request.ContentLength;
            return new ContentRangeInfo(0, length, (length ?? 0) > 0);
        }

        string value = header.Trim();
        const string prefix = "bytes ";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[prefix.Length..];
        }

        int slash = value.IndexOf('/');
        string rangePart = slash >= 0 ? value[..slash] : value;
        string totalPart = slash >= 0 ? value[(slash + 1)..] : "*";

        long? total = totalPart == "*"
            ? null
            : long.Parse(totalPart, CultureInfo.InvariantCulture);

        if (rangePart == "*")
        {
            // Status query / finalize probe: no payload in this request.
            return new ContentRangeInfo(0, total, false);
        }

        int dash = rangePart.IndexOf('-');
        long start = long.Parse(rangePart[..dash], CultureInfo.InvariantCulture);
        return new ContentRangeInfo(start, total, true);
    }

    private readonly record struct ContentRangeInfo(long Start, long? Total, bool HasData);
}
