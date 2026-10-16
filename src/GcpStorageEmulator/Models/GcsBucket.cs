using System.Text.Json.Serialization;

namespace GcpStorageEmulator.Models;

/// <summary>GCS <c>storage#bucket</c> resource. Numeric ids are strings, matching the real API.</summary>
public sealed class GcsBucket
{
    [JsonPropertyName("kind")]
    public string Kind => "storage#bucket";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("selfLink")]
    public string? SelfLink { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("projectNumber")]
    public string? ProjectNumber { get; init; }

    [JsonPropertyName("metageneration")]
    public required string Metageneration { get; init; }

    [JsonPropertyName("location")]
    public required string Location { get; init; }

    [JsonPropertyName("storageClass")]
    public required string StorageClass { get; init; }

    [JsonPropertyName("etag")]
    public string? Etag { get; init; }

    [JsonPropertyName("timeCreated")]
    public required string TimeCreated { get; init; }

    [JsonPropertyName("updated")]
    public required string Updated { get; init; }
}

/// <summary>GCS <c>storage#buckets</c> list response.</summary>
public sealed class GcsBucketList
{
    [JsonPropertyName("kind")]
    public string Kind => "storage#buckets";

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; init; }

    [JsonPropertyName("items")]
    public required IReadOnlyList<GcsBucket> Items { get; init; }
}
