using System.Text.Json.Serialization;

namespace GcpStorageEmulator.Models;

/// <summary>Body of a bucket insert request (a subset of the GCS bucket resource).</summary>
public sealed class BucketCreateRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("storageClass")]
    public string? StorageClass { get; set; }
}

/// <summary>
/// Object metadata as supplied in a JSON body (resumable init, or the metadata part of a
/// multipart upload). Mirrors the fields of the GCS object resource we honour.
/// </summary>
public sealed class ObjectMetadataRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("bucket")]
    public string? Bucket { get; set; }

    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("contentEncoding")]
    public string? ContentEncoding { get; set; }

    [JsonPropertyName("contentDisposition")]
    public string? ContentDisposition { get; set; }

    [JsonPropertyName("contentLanguage")]
    public string? ContentLanguage { get; set; }

    [JsonPropertyName("cacheControl")]
    public string? CacheControl { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}
