namespace GcpStorageEmulator.Storage;

/// <summary>Internal representation of a bucket, persisted as <c>.bucket.json</c>.</summary>
public sealed class BucketRecord
{
    public required string Name { get; init; }

    public long Metageneration { get; set; } = 1;

    public DateTimeOffset TimeCreated { get; set; }

    public DateTimeOffset Updated { get; set; }

    public string Location { get; set; } = "US";

    public string StorageClass { get; set; } = "STANDARD";

    public string? ProjectId { get; set; }
}
