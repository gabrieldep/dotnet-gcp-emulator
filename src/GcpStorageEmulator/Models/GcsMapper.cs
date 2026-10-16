using System.Globalization;
using GcpStorageEmulator.Storage;

namespace GcpStorageEmulator.Models;

/// <summary>Maps internal store records to the GCS JSON resource shapes.</summary>
public static class GcsMapper
{
    public static GcsBucket ToBucket(BucketRecord record, string baseUrl) => new()
    {
        Id = record.Name,
        Name = record.Name,
        SelfLink = $"{baseUrl}/storage/v1/b/{Uri.EscapeDataString(record.Name)}",
        ProjectNumber = ProjectNumber(record.ProjectId),
        Metageneration = record.Metageneration.ToString(CultureInfo.InvariantCulture),
        Location = record.Location,
        StorageClass = record.StorageClass,
        Etag = "CAE=",
        TimeCreated = GcsJson.Rfc3339(record.TimeCreated),
        Updated = GcsJson.Rfc3339(record.Updated),
    };

    public static GcsObject ToObject(ObjectRecord record, string baseUrl)
    {
        string encodedName = Uri.EscapeDataString(record.Name);
        string encodedBucket = Uri.EscapeDataString(record.Bucket);
        return new GcsObject
        {
            Id = $"{record.Bucket}/{record.Name}/{record.Generation}",
            Name = record.Name,
            Bucket = record.Bucket,
            SelfLink = $"{baseUrl}/storage/v1/b/{encodedBucket}/o/{encodedName}",
            MediaLink = $"{baseUrl}/download/storage/v1/b/{encodedBucket}/o/{encodedName}?generation={record.Generation}&alt=media",
            Generation = record.Generation.ToString(CultureInfo.InvariantCulture),
            Metageneration = record.Metageneration.ToString(CultureInfo.InvariantCulture),
            ContentType = record.ContentType,
            StorageClass = record.StorageClass,
            Size = record.Size.ToString(CultureInfo.InvariantCulture),
            Md5Hash = NullIfEmpty(record.Md5Hash),
            Crc32c = NullIfEmpty(record.Crc32c),
            Etag = record.Etag,
            TimeCreated = GcsJson.Rfc3339(record.TimeCreated),
            Updated = GcsJson.Rfc3339(record.Updated),
            ContentEncoding = record.ContentEncoding,
            ContentDisposition = record.ContentDisposition,
            ContentLanguage = record.ContentLanguage,
            CacheControl = record.CacheControl,
            Metadata = record.Metadata is { Count: > 0 } ? record.Metadata : null,
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>Deterministic 12-digit pseudo project number derived from the project id.</summary>
    private static string ProjectNumber(string? projectId)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            return "000000000000";
        }

        ulong hash = 1469598103934665603UL; // FNV-1a 64-bit
        foreach (char c in projectId)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }

        return (hash % 1_000_000_000_000UL).ToString("D12", CultureInfo.InvariantCulture);
    }
}
