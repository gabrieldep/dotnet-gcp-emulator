using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GcpStorageEmulator.Tests.Support;

namespace GcpStorageEmulator.Tests;

public class ObjectApiTests
{
    private static async Task CreateBucketAsync(EmulatorFixture fx, string bucket) =>
        await fx.Client.PostAsJsonAsync("/storage/v1/b?project=demo", new { name = bucket });

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static HttpContent MediaBody(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    [Fact]
    public async Task MediaUpload_ThenMetadataAndDownload()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await CreateBucketAsync(fx, "b");
        byte[] payload = Encoding.UTF8.GetBytes("hello world");

        HttpResponseMessage upload = await fx.Client.PostAsync(
            "/upload/storage/v1/b/b/o?uploadType=media&name=greeting.txt",
            MediaBody(payload, "text/plain"));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        JsonElement obj = await ReadJsonAsync(upload);
        Assert.Equal("storage#object", obj.GetProperty("kind").GetString());
        Assert.Equal("greeting.txt", obj.GetProperty("name").GetString());
        Assert.Equal("b", obj.GetProperty("bucket").GetString());
        Assert.Equal(payload.Length.ToString(), obj.GetProperty("size").GetString());
        Assert.Equal("text/plain", obj.GetProperty("contentType").GetString());
        Assert.False(string.IsNullOrEmpty(obj.GetProperty("generation").GetString()));
        Assert.False(string.IsNullOrEmpty(obj.GetProperty("md5Hash").GetString()));
        Assert.False(string.IsNullOrEmpty(obj.GetProperty("crc32c").GetString()));

        // Metadata round-trip
        JsonElement meta = await ReadJsonAsync(await fx.Client.GetAsync("/storage/v1/b/b/o/greeting.txt"));
        Assert.Equal("greeting.txt", meta.GetProperty("name").GetString());

        // Media download
        HttpResponseMessage download = await fx.Client.GetAsync("/storage/v1/b/b/o/greeting.txt?alt=media");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("text/plain", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(payload, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task MediaUpload_NameWithSlashes_RoundTrips()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await CreateBucketAsync(fx, "b");
        byte[] payload = Encoding.UTF8.GetBytes("nested content");
        string encoded = Uri.EscapeDataString("images/logo.png");

        HttpResponseMessage upload = await fx.Client.PostAsync(
            $"/upload/storage/v1/b/b/o?uploadType=media&name={encoded}",
            MediaBody(payload, "image/png"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        JsonElement obj = await ReadJsonAsync(upload);
        Assert.Equal("images/logo.png", obj.GetProperty("name").GetString());

        // Download via the %2F-encoded path tests raw-target extraction.
        HttpResponseMessage download = await fx.Client.GetAsync($"/storage/v1/b/b/o/{encoded}?alt=media");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(payload, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task MultipartUpload_StoresMetadataAndBytes()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await CreateBucketAsync(fx, "b");

        const string boundary = "test-boundary-123";
        const string metadataJson = """{"name":"doc.json","contentType":"application/json","metadata":{"owner":"alice"}}""";
        byte[] media = Encoding.UTF8.GetBytes("""{"hello":"world"}""");

        var sb = new StringBuilder();
        sb.Append("--").Append(boundary).Append("\r\n");
        sb.Append("Content-Type: application/json; charset=UTF-8\r\n\r\n");
        sb.Append(metadataJson).Append("\r\n");
        sb.Append("--").Append(boundary).Append("\r\n");
        sb.Append("Content-Type: application/json\r\n\r\n");
        sb.Append(Encoding.UTF8.GetString(media)).Append("\r\n");
        sb.Append("--").Append(boundary).Append("--\r\n");

        var body = new ByteArrayContent(Encoding.UTF8.GetBytes(sb.ToString()));
        body.Headers.ContentType = new MediaTypeHeaderValue("multipart/related") { Parameters = { new NameValueHeaderValue("boundary", boundary) } };

        HttpResponseMessage upload = await fx.Client.PostAsync(
            "/upload/storage/v1/b/b/o?uploadType=multipart", body);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        JsonElement obj = await ReadJsonAsync(upload);
        Assert.Equal("doc.json", obj.GetProperty("name").GetString());
        Assert.Equal("application/json", obj.GetProperty("contentType").GetString());
        Assert.Equal("alice", obj.GetProperty("metadata").GetProperty("owner").GetString());

        HttpResponseMessage download = await fx.Client.GetAsync("/storage/v1/b/b/o/doc.json?alt=media");
        Assert.Equal(media, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetObject_Missing_Returns404WithGcpErrorShape()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await CreateBucketAsync(fx, "b");

        HttpResponseMessage response = await fx.Client.GetAsync("/storage/v1/b/b/o/ghost.txt");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        JsonElement err = await ReadJsonAsync(response);
        Assert.Equal(404, err.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal("notFound", err.GetProperty("error").GetProperty("errors")[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Upload_ToMissingBucket_Returns404()
    {
        await using var fx = await EmulatorFixture.StartAsync();

        HttpResponseMessage upload = await fx.Client.PostAsync(
            "/upload/storage/v1/b/ghost/o?uploadType=media&name=x.txt",
            MediaBody(Encoding.UTF8.GetBytes("x"), "text/plain"));

        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
    }
}
