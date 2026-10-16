namespace GcpStorageEmulator.Storage;

/// <summary>
/// State for an in-flight resumable upload. Persisted so that a chunked upload can
/// survive a process restart between <c>PUT</c> calls.
/// </summary>
public sealed class ResumableSession
{
    public required string Id { get; init; }

    public required string Bucket { get; init; }

    public required string ObjectName { get; init; }

    public ObjectWriteOptions Options { get; set; } = new();

    /// <summary>Number of contiguous bytes persisted so far.</summary>
    public long BytesReceived { get; set; }

    /// <summary>Total object size once the client has declared it; null while unknown.</summary>
    public long? DeclaredTotalSize { get; set; }

    public DateTimeOffset Created { get; set; }
}
