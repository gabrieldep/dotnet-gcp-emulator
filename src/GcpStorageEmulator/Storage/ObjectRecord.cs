namespace GcpStorageEmulator.Storage;

/// <summary>Internal representation of an object's metadata, persisted alongside its bytes.</summary>
public sealed class ObjectRecord
{
    public required string Bucket { get; init; }

    public required string Name { get; init; }

    public long Size { get; set; }

    public string ContentType { get; set; } = "application/octet-stream";

    public long Generation { get; set; }

    public long Metageneration { get; set; } = 1;

    public DateTimeOffset TimeCreated { get; set; }

    public DateTimeOffset Updated { get; set; }

    /// <summary>Base64-encoded MD5 digest of the content.</summary>
    public string Md5Hash { get; set; } = string.Empty;

    /// <summary>Base64-encoded big-endian CRC32C of the content.</summary>
    public string Crc32c { get; set; } = string.Empty;

    public string Etag { get; set; } = string.Empty;

    public string StorageClass { get; set; } = "STANDARD";

    /// <summary>User-supplied custom metadata (the GCS <c>metadata</c> map).</summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>Optional content encoding/disposition/language carried through from upload.</summary>
    public string? ContentEncoding { get; set; }

    public string? ContentDisposition { get; set; }

    public string? ContentLanguage { get; set; }

    public string? CacheControl { get; set; }
}
