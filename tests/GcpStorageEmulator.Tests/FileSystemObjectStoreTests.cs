using System.Security.Cryptography;
using System.Text;
using GcpStorageEmulator.Storage;
using GcpStorageEmulator.Tests.Support;

namespace GcpStorageEmulator.Tests;

public class FileSystemObjectStoreTests
{
    private static FileSystemObjectStore NewStore(string root) => new(root);

    private static ObjectRecord Put(IObjectStore store, string bucket, string name, byte[] content, string? contentType = null, Dictionary<string, string>? metadata = null)
    {
        using var ms = new MemoryStream(content);
        return store.PutObject(bucket, name, new ObjectWriteOptions { ContentType = contentType, Metadata = metadata }, ms);
    }

    private static byte[] ReadAll(Stream? stream)
    {
        Assert.NotNull(stream);
        using (stream)
        using (var ms = new MemoryStream())
        {
            stream!.CopyTo(ms);
            return ms.ToArray();
        }
    }

    [Fact]
    public void CreateBucket_PersistsAndIsDiscoverable()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);

        BucketRecord bucket = store.CreateBucket("my-bucket");

        Assert.Equal("my-bucket", bucket.Name);
        Assert.True(store.BucketExists("my-bucket"));
        Assert.True(Directory.Exists(Path.Combine(dir.Path, "my-bucket")));
        Assert.NotNull(store.GetBucket("my-bucket"));
    }

    [Fact]
    public void ListBuckets_ReturnsAllSortedOrdinally()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);
        store.CreateBucket("zeta");
        store.CreateBucket("alpha");
        store.CreateBucket("mid");

        string[] names = store.ListBuckets().Select(b => b.Name).ToArray();

        Assert.Equal(new[] { "alpha", "mid", "zeta" }, names);
    }

    [Fact]
    public void DeleteBucket_WhenEmpty_Removes()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);
        store.CreateBucket("b");

        Assert.True(store.DeleteBucket("b"));
        Assert.False(store.BucketExists("b"));
        Assert.False(store.DeleteBucket("missing"));
    }

    [Fact]
    public void DeleteBucket_WhenNotEmpty_Throws()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);
        store.CreateBucket("b");
        Put(store, "b", "file.txt", Encoding.UTF8.GetBytes("hi"));

        Assert.Throws<BucketNotEmptyException>(() => store.DeleteBucket("b"));
    }

    [Fact]
    public void PutObject_ToMissingBucket_Throws()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);

        Assert.Throws<BucketNotFoundException>(() => Put(store, "nope", "a.txt", new byte[] { 1 }));
    }

    [Fact]
    public void PutAndGetObject_RoundTripsMetadataAndBytes()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);
        store.CreateBucket("b");
        byte[] content = Encoding.UTF8.GetBytes("hello world");

        ObjectRecord record = Put(store, "b", "greeting.txt", content, "text/plain");

        Assert.Equal("b", record.Bucket);
        Assert.Equal("greeting.txt", record.Name);
        Assert.Equal(content.Length, record.Size);
        Assert.Equal("text/plain", record.ContentType);
        Assert.True(record.Generation > 0);
        Assert.Equal(Convert.ToBase64String(MD5.HashData(content)), record.Md5Hash);
        Assert.Equal(Crc32C.ToBase64(Crc32C.Compute(content)), record.Crc32c);
        Assert.NotEmpty(record.Etag);

        Assert.Equal(content, ReadAll(store.OpenRead("b", "greeting.txt")));

        ObjectRecord? fetched = store.GetObject("b", "greeting.txt");
        Assert.NotNull(fetched);
        Assert.Equal(record.Generation, fetched!.Generation);
        Assert.Equal(record.Md5Hash, fetched.Md5Hash);
    }

    [Fact]
    public void PutObject_DefaultsContentTypeToOctetStream()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);
        store.CreateBucket("b");

        ObjectRecord record = Put(store, "b", "x.bin", new byte[] { 1, 2, 3 });

        Assert.Equal("application/octet-stream", record.ContentType);
    }

    [Fact]
    public void PutObject_CustomMetadataRoundTrips()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);
        store.CreateBucket("b");
        var metadata = new Dictionary<string, string> { ["owner"] = "alice", ["env"] = "dev" };

        Put(store, "b", "x", new byte[] { 1 }, metadata: metadata);

        ObjectRecord? fetched = store.GetObject("b", "x");
        Assert.NotNull(fetched!.Metadata);
        Assert.Equal("alice", fetched.Metadata!["owner"]);
        Assert.Equal("dev", fetched.Metadata["env"]);
    }

    [Fact]
    public void PutObject_Overwrite_AssignsNewGeneration()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);
        store.CreateBucket("b");

        ObjectRecord first = Put(store, "b", "x", Encoding.UTF8.GetBytes("v1"));
        ObjectRecord second = Put(store, "b", "x", Encoding.UTF8.GetBytes("v2-longer"));

        Assert.True(second.Generation > first.Generation);
        Assert.Equal(Encoding.UTF8.GetBytes("v2-longer"), ReadAll(store.OpenRead("b", "x")));
    }

    [Fact]
    public void Objects_NamedAAndASlashB_Coexist()
    {
        // The crucial GCS case a naive nested-directory layout cannot represent.
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);
        store.CreateBucket("b");

        Put(store, "b", "a", Encoding.UTF8.GetBytes("I am object a"));
        Put(store, "b", "a/b", Encoding.UTF8.GetBytes("I am object a/b"));

        Assert.Equal(Encoding.UTF8.GetBytes("I am object a"), ReadAll(store.OpenRead("b", "a")));
        Assert.Equal(Encoding.UTF8.GetBytes("I am object a/b"), ReadAll(store.OpenRead("b", "a/b")));

        string[] names = store.ListObjects("b").Select(o => o.Name).ToArray();
        Assert.Equal(new[] { "a", "a/b" }, names);
    }

    [Fact]
    public void DeleteObject_RemovesMetadataAndBytes()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);
        store.CreateBucket("b");
        Put(store, "b", "gone.txt", new byte[] { 9 });

        Assert.True(store.DeleteObject("b", "gone.txt"));
        Assert.False(store.ObjectExists("b", "gone.txt"));
        Assert.Null(store.OpenRead("b", "gone.txt"));
        Assert.False(store.DeleteObject("b", "gone.txt"));
    }

    [Fact]
    public void State_SurvivesReopeningTheStore()
    {
        using var dir = new TempDirectory();
        byte[] content = Encoding.UTF8.GetBytes("durable bytes");

        ObjectRecord original;
        {
            var store = NewStore(dir.Path);
            store.CreateBucket("b");
            original = Put(store, "b", "nested/file.txt", content, "text/plain");
        }

        // Brand new store instance over the same root — simulates a process restart.
        var reopened = NewStore(dir.Path);

        Assert.True(reopened.BucketExists("b"));
        ObjectRecord? fetched = reopened.GetObject("b", "nested/file.txt");
        Assert.NotNull(fetched);
        Assert.Equal(original.Generation, fetched!.Generation);
        Assert.Equal(original.Md5Hash, fetched.Md5Hash);
        Assert.Equal("text/plain", fetched.ContentType);
        Assert.Equal(content, ReadAll(reopened.OpenRead("b", "nested/file.txt")));
    }

    [Fact]
    public void ResumableSession_SingleChunk_Finalises()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);
        store.CreateBucket("b");
        byte[] content = Encoding.UTF8.GetBytes("resumable payload");

        ResumableSession session = store.CreateResumableSession("b", "r.txt", new ObjectWriteOptions { ContentType = "text/plain" });
        Assert.Equal(0, session.BytesReceived);

        using (var ms = new MemoryStream(content))
        {
            ResumableSession updated = store.WriteResumableChunk(session.Id, 0, ms);
            Assert.Equal(content.Length, updated.BytesReceived);
        }

        ObjectRecord record = store.CompleteResumableSession(session.Id);

        Assert.Equal(content.Length, record.Size);
        Assert.Equal(content, ReadAll(store.OpenRead("b", "r.txt")));
        Assert.Null(store.GetResumableSession(session.Id));
    }

    [Fact]
    public void ResumableSession_MultipleChunks_Concatenate()
    {
        using var dir = new TempDirectory();
        var store = NewStore(dir.Path);
        store.CreateBucket("b");
        byte[] part1 = Encoding.UTF8.GetBytes("first-");
        byte[] part2 = Encoding.UTF8.GetBytes("second");

        ResumableSession session = store.CreateResumableSession("b", "chunked", new ObjectWriteOptions());
        using (var ms = new MemoryStream(part1))
        {
            store.WriteResumableChunk(session.Id, 0, ms);
        }

        using (var ms = new MemoryStream(part2))
        {
            ResumableSession updated = store.WriteResumableChunk(session.Id, part1.Length, ms);
            Assert.Equal(part1.Length + part2.Length, updated.BytesReceived);
        }

        store.CompleteResumableSession(session.Id);

        Assert.Equal(
            part1.Concat(part2).ToArray(),
            ReadAll(store.OpenRead("b", "chunked")));
    }
}
