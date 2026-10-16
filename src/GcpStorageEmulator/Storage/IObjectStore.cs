namespace GcpStorageEmulator.Storage;

/// <summary>
/// Abstraction over the persistence layer for buckets and objects. The HTTP layer depends
/// only on this interface, so alternative backends (in-memory, cloud, ...) could be dropped in.
/// </summary>
public interface IObjectStore
{
    // ---- Buckets ---------------------------------------------------------------------

    bool BucketExists(string bucket);

    BucketRecord? GetBucket(string bucket);

    /// <summary>Creates a bucket. Returns the existing record if it already exists.</summary>
    BucketRecord CreateBucket(string bucket, string? location = null, string? storageClass = null, string? projectId = null);

    IReadOnlyList<BucketRecord> ListBuckets();

    /// <summary>Deletes an empty bucket. Returns false if it does not exist.</summary>
    /// <exception cref="BucketNotEmptyException">The bucket still contains objects.</exception>
    bool DeleteBucket(string bucket);

    // ---- Objects ---------------------------------------------------------------------

    ObjectRecord? GetObject(string bucket, string objectName);

    bool ObjectExists(string bucket, string objectName);

    /// <summary>Writes (or overwrites) an object from a stream, assigning a new generation.</summary>
    ObjectRecord PutObject(string bucket, string objectName, ObjectWriteOptions options, Stream content);

    /// <summary>Opens the raw content of an object for reading, or null when absent.</summary>
    Stream? OpenRead(string bucket, string objectName);

    IReadOnlyList<ObjectRecord> ListObjects(string bucket);

    bool DeleteObject(string bucket, string objectName);

    // ---- Resumable uploads -----------------------------------------------------------

    ResumableSession CreateResumableSession(string bucket, string objectName, ObjectWriteOptions options);

    ResumableSession? GetResumableSession(string sessionId);

    /// <summary>
    /// Persists a chunk of body bytes for a resumable session at the given absolute offset
    /// and returns the updated session (with the new <see cref="ResumableSession.BytesReceived"/>).
    /// </summary>
    ResumableSession WriteResumableChunk(string sessionId, long offset, Stream data);

    /// <summary>Finalises a resumable session into a stored object and discards the session.</summary>
    ObjectRecord CompleteResumableSession(string sessionId);

    void AbortResumableSession(string sessionId);
}

/// <summary>Thrown when attempting to delete a bucket that still contains objects.</summary>
public sealed class BucketNotEmptyException(string bucket)
    : Exception($"The bucket '{bucket}' is not empty.")
{
    public string Bucket { get; } = bucket;
}

/// <summary>Thrown when an operation targets a bucket that does not exist.</summary>
public sealed class BucketNotFoundException(string bucket)
    : Exception($"The bucket '{bucket}' does not exist.")
{
    public string Bucket { get; } = bucket;
}

/// <summary>Thrown when a resumable upload session id is unknown or already finalised.</summary>
public sealed class ResumableSessionNotFoundException(string sessionId)
    : Exception($"Unknown resumable session '{sessionId}'.")
{
    public string SessionId { get; } = sessionId;
}
