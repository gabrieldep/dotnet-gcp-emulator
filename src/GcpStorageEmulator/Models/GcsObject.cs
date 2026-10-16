using System.Text.Json.Serialization;

namespace GcpStorageEmulator.Models;

/// <summary>GCS <c>storage#object</c> resource.</summary>
public sealed class GcsObject
{
    [JsonPropertyName("kind")]
    public string Kind => "storage#object";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("selfLink")]
    public string? SelfLink { get; init; }

    [JsonPropertyName("mediaLink")]
    public string? MediaLink { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("bucket")]
    public required string Bucket { get; init; }

    [JsonPropertyName("generation")]
    public required string Generation { get; init; }

    [JsonPropertyName("metageneration")]
    public required string Metageneration { get; init; }

    [JsonPropertyName("contentType")]
    public string? ContentType { get; init; }

    [JsonPropertyName("storageClass")]
    public required string StorageClass { get; init; }

    [JsonPropertyName("size")]
    public required string Size { get; init; }

    [JsonPropertyName("md5Hash")]
    public string? Md5Hash { get; init; }

    [JsonPropertyName("crc32c")]
    public string? Crc32c { get; init; }

    [JsonPropertyName("etag")]
    public string? Etag { get; init; }

    [JsonPropertyName("timeCreated")]
    public required string TimeCreated { get; init; }

    [JsonPropertyName("updated")]
    public required string Updated { get; init; }

    [JsonPropertyName("contentEncoding")]
    public string? ContentEncoding { get; init; }

    [JsonPropertyName("contentDisposition")]
    public string? ContentDisposition { get; init; }

    [JsonPropertyName("contentLanguage")]
    public string? ContentLanguage { get; init; }

    [JsonPropertyName("cacheControl")]
    public string? CacheControl { get; init; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>GCS <c>storage#objects</c> list response.</summary>
public sealed class GcsObjectList
{
    [JsonPropertyName("kind")]
    public string Kind => "storage#objects";

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; init; }

    [JsonPropertyName("prefixes")]
    public IReadOnlyList<string>? Prefixes { get; init; }

    [JsonPropertyName("items")]
    public IReadOnlyList<GcsObject>? Items { get; init; }
}
