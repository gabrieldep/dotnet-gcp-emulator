using System.Text;
using GcpStorageEmulator.Tests.Support;
using Google.Api.Gax;
using Google.Cloud.Storage.V1;

namespace GcpStorageEmulator.Tests;

/// <summary>
/// End-to-end tests that drive the emulator with the real Google.Cloud.Storage.V1 SDK, pointed
/// at it through <c>STORAGE_EMULATOR_HOST</c> exactly as production code would. These exercise
/// the resumable upload protocol the SDK uses by default.
/// </summary>
[Collection(nameof(SdkCollection))]
public class SdkCompatibilityTests
{
    private static StorageClient CreateClient(EmulatorFixture fx)
    {
        Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", fx.Host);
        return new StorageClientBuilder { EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    }

    [Fact]
    public async Task UploadAndDownload_SingleShotResumable()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        string? previous = Environment.GetEnvironmentVariable("STORAGE_EMULATOR_HOST");
        try
        {
            StorageClient client = CreateClient(fx);
            await client.CreateBucketAsync("test-project", "sdk-bucket");

            byte[] content = Encoding.UTF8.GetBytes("resumable upload via the official SDK");
            using var source = new MemoryStream(content);
            var uploaded = await client.UploadObjectAsync("sdk-bucket", "hello.txt", "text/plain", source);

            Assert.Equal((ulong)content.Length, uploaded.Size);
            Assert.Equal("text/plain", uploaded.ContentType);

            using var destination = new MemoryStream();
            await client.DownloadObjectAsync("sdk-bucket", "hello.txt", destination);
            Assert.Equal(content, destination.ToArray());
        }
        finally
        {
            Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", previous);
        }
    }

    [Fact]
    public async Task UploadAndDownload_ChunkedResumable()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        string? previous = Environment.GetEnvironmentVariable("STORAGE_EMULATOR_HOST");
        try
        {
            StorageClient client = CreateClient(fx);
            await client.CreateBucketAsync("test-project", "chunk-bucket");

            // ~650 KB with a 256 KB chunk size forces several PUTs and the 308 "resume incomplete" path.
            var content = new byte[650_000];
            new Random(1234).NextBytes(content);
            using var source = new MemoryStream(content);

            var options = new UploadObjectOptions { ChunkSize = UploadObjectOptions.MinimumChunkSize };
            var uploaded = await client.UploadObjectAsync("chunk-bucket", "big.bin", "application/octet-stream", source, options);

            Assert.Equal((ulong)content.Length, uploaded.Size);

            using var destination = new MemoryStream();
            await client.DownloadObjectAsync("chunk-bucket", "big.bin", destination);
            Assert.Equal(content, destination.ToArray());
        }
        finally
        {
            Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", previous);
        }
    }

    [Fact]
    public async Task ListAndDelete_ViaSdk()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        string? previous = Environment.GetEnvironmentVariable("STORAGE_EMULATOR_HOST");
        try
        {
            StorageClient client = CreateClient(fx);
            await client.CreateBucketAsync("test-project", "list-bucket");

            foreach (string name in new[] { "a.txt", "b.txt", "nested/c.txt" })
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(name));
                await client.UploadObjectAsync("list-bucket", name, "text/plain", ms);
            }

            var names = new List<string>();
            await foreach (var o in client.ListObjectsAsync("list-bucket", string.Empty))
            {
                names.Add(o.Name);
            }

            Assert.Contains("a.txt", names);
            Assert.Contains("b.txt", names);
            Assert.Contains("nested/c.txt", names);

            await client.DeleteObjectAsync("list-bucket", "a.txt");
            using var destination = new MemoryStream();
            var ex = await Assert.ThrowsAsync<Google.GoogleApiException>(
                () => client.DownloadObjectAsync("list-bucket", "a.txt", destination));
            Assert.Equal(System.Net.HttpStatusCode.NotFound, ex.HttpStatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", previous);
        }
    }
}

[CollectionDefinition(nameof(SdkCollection), DisableParallelization = true)]
public sealed class SdkCollection;
