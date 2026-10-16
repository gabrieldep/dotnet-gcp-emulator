using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GcpStorageEmulator.Configuration;
using Microsoft.Extensions.Options;

namespace GcpStorageEmulator.Storage;

/// <summary>
/// Filesystem implementation of <see cref="IObjectStore"/>.
/// <para>On-disk layout (rooted at <c>StorageRoot</c>):</para>
/// <code>
/// {root}/
///   {bucket}/
///     .bucket.json              bucket metadata
///     .objects/
///       {encodedName}.bin       object bytes
///       {encodedName}.json      object metadata (ObjectRecord)
///   .uploads/
///     {sessionId}.session.json  resumable session state
///     {sessionId}.part          resumable partial bytes
/// </code>
/// Object names are flattened with <see cref="StorageNameEncoder"/> so that names containing
/// '/' never create directories and the GCS case of coexisting "a" and "a/b" is representable.
/// </summary>
public sealed class FileSystemObjectStore : IObjectStore
{
    private const string BucketMetaFile = ".bucket.json";
    private const string ObjectsDirName = ".objects";
    private const string UploadsDirName = ".uploads";
    private const string BinExt = ".bin";
    private const string MetaExt = ".json";

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _root;
    private long _lastGeneration;

    public FileSystemObjectStore(IOptions<EmulatorOptions> options)
        : this(options.Value.StorageRoot)
    {
    }

    public FileSystemObjectStore(string storageRoot)
    {
        _root = Path.GetFullPath(storageRoot);
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, UploadsDirName));
    }

    /// <summary>The resolved absolute storage root (useful for diagnostics).</summary>
    public string Root => _root;

    // ---- Buckets ---------------------------------------------------------------------

    public bool BucketExists(string bucket)
    {
        lock (_gate)
        {
            return File.Exists(Path.Combine(BucketDir(bucket), BucketMetaFile));
        }
    }

    public BucketRecord? GetBucket(string bucket)
    {
        lock (_gate)
        {
            string meta = Path.Combine(BucketDir(bucket), BucketMetaFile);
            return File.Exists(meta) ? ReadJson<BucketRecord>(meta) : null;
        }
    }

    public BucketRecord CreateBucket(string bucket, string? location = null, string? storageClass = null, string? projectId = null)
    {
        lock (_gate)
        {
            string dir = BucketDir(bucket);
            string meta = Path.Combine(dir, BucketMetaFile);
            if (File.Exists(meta))
            {
                return ReadJson<BucketRecord>(meta);
            }

            Directory.CreateDirectory(Path.Combine(dir, ObjectsDirName));
            var now = DateTimeOffset.UtcNow;
            var record = new BucketRecord
            {
                Name = bucket,
                TimeCreated = now,
                Updated = now,
                Location = location ?? "US",
                StorageClass = storageClass ?? "STANDARD",
                ProjectId = projectId,
            };
            WriteJson(meta, record);
            return record;
        }
    }

    public IReadOnlyList<BucketRecord> ListBuckets()
    {
        lock (_gate)
        {
            var list = new List<BucketRecord>();
            foreach (string dir in Directory.EnumerateDirectories(_root))
            {
                string meta = Path.Combine(dir, BucketMetaFile);
                if (File.Exists(meta))
                {
                    list.Add(ReadJson<BucketRecord>(meta));
                }
            }

            list.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }
    }

    public bool DeleteBucket(string bucket)
    {
        lock (_gate)
        {
            string dir = BucketDir(bucket);
            if (!File.Exists(Path.Combine(dir, BucketMetaFile)))
            {
                return false;
            }

            string objectsDir = Path.Combine(dir, ObjectsDirName);
            if (Directory.Exists(objectsDir)
                && Directory.EnumerateFiles(objectsDir, "*" + MetaExt).Any())
            {
                throw new BucketNotEmptyException(bucket);
            }

            Directory.Delete(dir, recursive: true);
            return true;
        }
    }

    // ---- Objects ---------------------------------------------------------------------

    public ObjectRecord? GetObject(string bucket, string objectName)
    {
        lock (_gate)
        {
            string meta = ObjectMetaPath(bucket, objectName);
            return File.Exists(meta) ? ReadJson<ObjectRecord>(meta) : null;
        }
    }

    public bool ObjectExists(string bucket, string objectName)
    {
        lock (_gate)
        {
            return File.Exists(ObjectMetaPath(bucket, objectName));
        }
    }

    public ObjectRecord PutObject(string bucket, string objectName, ObjectWriteOptions options, Stream content)
    {
        lock (_gate)
        {
            string dir = BucketDir(bucket);
            if (!File.Exists(Path.Combine(dir, BucketMetaFile)))
            {
                throw new BucketNotFoundException(bucket);
            }

            string objectsDir = Path.Combine(dir, ObjectsDirName);
            Directory.CreateDirectory(objectsDir);

            string binPath = ObjectBinPath(bucket, objectName);
            string tmp = binPath + ".tmp-" + Guid.NewGuid().ToString("N");

            long size;
            string md5;
            string crc;
            using (FileStream dst = File.Create(tmp))
            {
                (size, md5, crc) = CopyWithHashes(content, dst);
            }

            File.Move(tmp, binPath, overwrite: true);
            ObjectRecord record = BuildRecord(bucket, objectName, options, size, md5, crc);
            WriteJson(ObjectMetaPath(bucket, objectName), record);
            return record;
        }
    }

    public Stream? OpenRead(string bucket, string objectName)
    {
        lock (_gate)
        {
            string bin = ObjectBinPath(bucket, objectName);
            if (!File.Exists(ObjectMetaPath(bucket, objectName)) || !File.Exists(bin))
            {
                return null;
            }

            return new FileStream(bin, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }

    public IReadOnlyList<ObjectRecord> ListObjects(string bucket)
    {
        lock (_gate)
        {
            string objectsDir = Path.Combine(BucketDir(bucket), ObjectsDirName);
            if (!Directory.Exists(objectsDir))
            {
                return Array.Empty<ObjectRecord>();
            }

            var list = new List<ObjectRecord>();
            foreach (string file in Directory.EnumerateFiles(objectsDir, "*" + MetaExt))
            {
                list.Add(ReadJson<ObjectRecord>(file));
            }

            list.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }
    }

    public bool DeleteObject(string bucket, string objectName)
    {
        lock (_gate)
        {
            string meta = ObjectMetaPath(bucket, objectName);
            if (!File.Exists(meta))
            {
                return false;
            }

            File.Delete(meta);
            string bin = ObjectBinPath(bucket, objectName);
            if (File.Exists(bin))
            {
                File.Delete(bin);
            }

            return true;
        }
    }

    // ---- Resumable uploads -----------------------------------------------------------

    public ResumableSession CreateResumableSession(string bucket, string objectName, ObjectWriteOptions options)
    {
        lock (_gate)
        {
            if (!File.Exists(Path.Combine(BucketDir(bucket), BucketMetaFile)))
            {
                throw new BucketNotFoundException(bucket);
            }

            string id = Guid.NewGuid().ToString("N");
            var session = new ResumableSession
            {
                Id = id,
                Bucket = bucket,
                ObjectName = objectName,
                Options = options,
                BytesReceived = 0,
                Created = DateTimeOffset.UtcNow,
            };

            File.Create(PartPath(id)).Dispose();
            WriteJson(SessionPath(id), session);
            return session;
        }
    }

    public ResumableSession? GetResumableSession(string sessionId)
    {
        lock (_gate)
        {
            string path = SessionPath(sessionId);
            return File.Exists(path) ? ReadJson<ResumableSession>(path) : null;
        }
    }

    public ResumableSession WriteResumableChunk(string sessionId, long offset, Stream data)
    {
        lock (_gate)
        {
            ResumableSession session = ReadSessionOrThrow(sessionId);
            using (var fs = new FileStream(PartPath(sessionId), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                fs.Seek(offset, SeekOrigin.Begin);
                long written = CopyCounting(data, fs);
                session.BytesReceived = Math.Max(session.BytesReceived, offset + written);
            }

            WriteJson(SessionPath(sessionId), session);
            return session;
        }
    }

    public ObjectRecord CompleteResumableSession(string sessionId)
    {
        lock (_gate)
        {
            ResumableSession session = ReadSessionOrThrow(sessionId);
            string part = PartPath(sessionId);
            (long size, string md5, string crc) = HashFile(part);

            string binPath = ObjectBinPath(session.Bucket, session.ObjectName);
            Directory.CreateDirectory(Path.GetDirectoryName(binPath)!);
            File.Move(part, binPath, overwrite: true);

            ObjectRecord record = BuildRecord(session.Bucket, session.ObjectName, session.Options, size, md5, crc);
            WriteJson(ObjectMetaPath(session.Bucket, session.ObjectName), record);

            File.Delete(SessionPath(sessionId));
            return record;
        }
    }

    public void AbortResumableSession(string sessionId)
    {
        lock (_gate)
        {
            string part = PartPath(sessionId);
            string session = SessionPath(sessionId);
            if (File.Exists(part))
            {
                File.Delete(part);
            }

            if (File.Exists(session))
            {
                File.Delete(session);
            }
        }
    }

    // ---- Helpers ---------------------------------------------------------------------

    private ObjectRecord BuildRecord(string bucket, string objectName, ObjectWriteOptions options, long size, string md5, string crc)
    {
        var now = DateTimeOffset.UtcNow;
        long generation = NextGeneration();
        return new ObjectRecord
        {
            Bucket = bucket,
            Name = objectName,
            Size = size,
            ContentType = string.IsNullOrEmpty(options.ContentType) ? "application/octet-stream" : options.ContentType,
            Generation = generation,
            Metageneration = 1,
            TimeCreated = now,
            Updated = now,
            Md5Hash = md5,
            Crc32c = crc,
            Etag = MakeEtag(bucket, objectName, generation),
            Metadata = options.Metadata,
            ContentEncoding = options.ContentEncoding,
            ContentDisposition = options.ContentDisposition,
            ContentLanguage = options.ContentLanguage,
            CacheControl = options.CacheControl,
        };
    }

    private long NextGeneration()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
        long generation = Math.Max(_lastGeneration + 1, now);
        _lastGeneration = generation;
        return generation;
    }

    private static string MakeEtag(string bucket, string name, long generation) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{bucket}/{name}#{generation}"));

    private string BucketDir(string bucket)
    {
        if (string.IsNullOrEmpty(bucket)
            || bucket.StartsWith('.')
            || bucket.Contains('/')
            || bucket.Contains('\\')
            || bucket.Contains(".."))
        {
            throw new ArgumentException($"Invalid bucket name: '{bucket}'.", nameof(bucket));
        }

        return Path.Combine(_root, bucket);
    }

    private string ObjectMetaPath(string bucket, string name) =>
        Path.Combine(BucketDir(bucket), ObjectsDirName, StorageNameEncoder.Encode(name) + MetaExt);

    private string ObjectBinPath(string bucket, string name) =>
        Path.Combine(BucketDir(bucket), ObjectsDirName, StorageNameEncoder.Encode(name) + BinExt);

    private string PartPath(string sessionId) =>
        Path.Combine(_root, UploadsDirName, sessionId + ".part");

    private string SessionPath(string sessionId) =>
        Path.Combine(_root, UploadsDirName, sessionId + ".session" + MetaExt);

    private ResumableSession ReadSessionOrThrow(string sessionId)
    {
        string path = SessionPath(sessionId);
        if (!File.Exists(path))
        {
            throw new ResumableSessionNotFoundException(sessionId);
        }

        return ReadJson<ResumableSession>(path);
    }

    private static (long size, string md5B64, string crc32cB64) CopyWithHashes(Stream src, Stream dst)
    {
        using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        uint crc = 0;
        long total = 0;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
            {
                dst.Write(buffer, 0, read);
                md5.AppendData(buffer, 0, read);
                crc = Crc32C.Append(crc, buffer.AsSpan(0, read));
                total += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return (total, Convert.ToBase64String(md5.GetHashAndReset()), Crc32C.ToBase64(crc));
    }

    private static (long size, string md5B64, string crc32cB64) HashFile(string path)
    {
        using FileStream fs = File.OpenRead(path);
        using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        uint crc = 0;
        long total = 0;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                md5.AppendData(buffer, 0, read);
                crc = Crc32C.Append(crc, buffer.AsSpan(0, read));
                total += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return (total, Convert.ToBase64String(md5.GetHashAndReset()), Crc32C.ToBase64(crc));
    }

    private static long CopyCounting(Stream src, Stream dst)
    {
        long total = 0;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
            {
                dst.Write(buffer, 0, read);
                total += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return total;
    }

    private static T ReadJson<T>(string path)
    {
        using FileStream fs = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(fs, Json)
               ?? throw new InvalidDataException($"Corrupt metadata file: {path}");
    }

    private static void WriteJson<T>(string path, T value)
    {
        string tmp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        using (FileStream fs = File.Create(tmp))
        {
            JsonSerializer.Serialize(fs, value, Json);
        }

        File.Move(tmp, path, overwrite: true);
    }
}
