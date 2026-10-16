namespace GcpStorageEmulator.Storage;

/// <summary>Metadata supplied by a client when creating/overwriting an object.</summary>
public sealed class ObjectWriteOptions
{
    public string? ContentType { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }

    public string? ContentEncoding { get; set; }

    public string? ContentDisposition { get; set; }

    public string? ContentLanguage { get; set; }

    public string? CacheControl { get; set; }
}
