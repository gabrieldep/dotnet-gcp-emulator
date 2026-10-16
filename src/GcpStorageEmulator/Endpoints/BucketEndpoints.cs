using GcpStorageEmulator.Configuration;
using GcpStorageEmulator.Models;
using GcpStorageEmulator.Storage;
using Microsoft.Extensions.Options;

namespace GcpStorageEmulator.Endpoints;

/// <summary>GCS JSON API for buckets: insert, get, list, delete.</summary>
public static class BucketEndpoints
{
    public static void MapBucketEndpoints(this IEndpointRouteBuilder app)
    {
        foreach (string prefix in RoutePrefixes.Json)
        {
            app.MapPost($"{prefix}/b", CreateBucketAsync);
            app.MapGet($"{prefix}/b", ListBuckets);
            app.MapGet($"{prefix}/b/{{bucket}}", GetBucket);
            app.MapDelete($"{prefix}/b/{{bucket}}", DeleteBucket);
        }
    }

    private static async Task<IResult> CreateBucketAsync(HttpContext context, IObjectStore store, IOptions<EmulatorOptions> options)
    {
        BucketCreateRequest? body = null;
        if (context.Request.HasJsonContentType())
        {
            try
            {
                body = await context.Request.ReadFromJsonAsync<BucketCreateRequest>(GcsJson.Options);
            }
            catch
            {
                return GcsResults.BadRequest("parseError", "Could not parse the bucket resource.");
            }
        }

        string name = body?.Name ?? context.Request.Query["name"].ToString();
        if (string.IsNullOrWhiteSpace(name))
        {
            return GcsResults.BadRequest("required", "Required parameter: name");
        }

        if (store.BucketExists(name))
        {
            return GcsResults.Error(StatusCodes.Status409Conflict, "conflict",
                "You already own this bucket. Please select another name.");
        }

        string project = context.Request.Query["project"].ToString();
        string projectId = string.IsNullOrEmpty(project) ? options.Value.DefaultProjectId : project;
        BucketRecord record = store.CreateBucket(name, body?.Location, body?.StorageClass, projectId);
        return GcsResults.Json(GcsMapper.ToBucket(record, EmulatorUrls.BaseUrl(context, options.Value)));
    }

    private static IResult ListBuckets(HttpContext context, IObjectStore store, IOptions<EmulatorOptions> options)
    {
        string baseUrl = EmulatorUrls.BaseUrl(context, options.Value);
        List<GcsBucket> items = store.ListBuckets().Select(b => GcsMapper.ToBucket(b, baseUrl)).ToList();
        return GcsResults.Json(new GcsBucketList { Items = items });
    }

    private static IResult GetBucket(string bucket, HttpContext context, IObjectStore store, IOptions<EmulatorOptions> options)
    {
        BucketRecord? record = store.GetBucket(bucket);
        return record is null
            ? GcsResults.NotFound($"Not Found: {bucket}")
            : GcsResults.Json(GcsMapper.ToBucket(record, EmulatorUrls.BaseUrl(context, options.Value)));
    }

    private static IResult DeleteBucket(string bucket, IObjectStore store)
    {
        try
        {
            return store.DeleteBucket(bucket)
                ? Results.StatusCode(StatusCodes.Status204NoContent)
                : GcsResults.NotFound($"Not Found: {bucket}");
        }
        catch (BucketNotEmptyException)
        {
            return GcsResults.Error(StatusCodes.Status409Conflict, "conflict",
                "The bucket you tried to delete is not empty.");
        }
    }
}
