using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using GcpStorageEmulator.Tests.Support;

namespace GcpStorageEmulator.Tests;

public class PathStyleDownloadTests
{
    private static async Task CreateBucketAsync(EmulatorFixture fx, string bucket) =>
        await fx.Client.PostAsJsonAsync("/storage/v1/b?project=demo", new { name = bucket });

    private static async Task UploadAsync(EmulatorFixture fx, string bucket, string name, byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        HttpResponseMessage response = await fx.Client.PostAsync(
            $"/upload/storage/v1/b/{bucket}/o?uploadType=media&name={Uri.EscapeDataString(name)}", content);
        response.EnsureSuccessStatusCode();
    }

    // A V4-signed URL carries these query params; the emulator ignores them by design.
    private const string FakeSignatureQuery =
        "?X-Goog-Algorithm=GOOG4-RSA-SHA256&X-Goog-Credential=emulator%2F20260101%2Fauto%2Fstorage%2Fgoog4_request" +
        "&X-Goog-Date=20260101T000000Z&X-Goog-Expires=3600&X-Goog-SignedHeaders=host&X-Goog-Signature=deadbeefcafe";

    [Fact]
    public async Task PathStyle_ServesObject_IgnoringSignatureParams()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await CreateBucketAsync(fx, "signed-bucket");
        byte[] payload = Encoding.UTF8.GetBytes("content behind a signed url");
        await UploadAsync(fx, "signed-bucket", "report.txt", payload, "text/plain");

        HttpResponseMessage response = await fx.Client.GetAsync($"/signed-bucket/report.txt{FakeSignatureQuery}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(payload, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task PathStyle_SupportsObjectNamesWithSlashes()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await CreateBucketAsync(fx, "assets");
        byte[] payload = Encoding.UTF8.GetBytes("nested object bytes");
        await UploadAsync(fx, "assets", "a/deep/key.bin", payload, "application/octet-stream");

        string encoded = Uri.EscapeDataString("a/deep/key.bin");
        HttpResponseMessage response = await fx.Client.GetAsync($"/assets/{encoded}{FakeSignatureQuery}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(payload, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task PathStyle_Head_ReturnsHeadersWithoutBody()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await CreateBucketAsync(fx, "assets");
        byte[] payload = Encoding.UTF8.GetBytes("head me");
        await UploadAsync(fx, "assets", "h.txt", payload, "text/plain");

        using var request = new HttpRequestMessage(HttpMethod.Head, $"/assets/h.txt{FakeSignatureQuery}");
        HttpResponseMessage response = await fx.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(payload.Length, response.Content.Headers.ContentLength);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task PathStyle_MissingObject_Returns404()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await CreateBucketAsync(fx, "assets");

        HttpResponseMessage response = await fx.Client.GetAsync("/assets/not-here.txt");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PathStyleCatchAll_DoesNotShadowJsonApi()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await CreateBucketAsync(fx, "visible");

        // The literal /storage/v1/b route must still win over the catch-all.
        HttpResponseMessage list = await fx.Client.GetAsync("/storage/v1/b?project=demo");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("storage#buckets", await list.Content.ReadAsStringAsync());
    }
}
