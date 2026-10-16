using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GcpStorageEmulator.Tests.Support;

namespace GcpStorageEmulator.Tests;

public class ObjectListDeleteTests
{
    private static async Task CreateBucketAsync(EmulatorFixture fx, string bucket) =>
        await fx.Client.PostAsJsonAsync("/storage/v1/b?project=demo", new { name = bucket });

    private static async Task UploadAsync(EmulatorFixture fx, string bucket, string name)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(name));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        (await fx.Client.PostAsync(
            $"/upload/storage/v1/b/{bucket}/o?uploadType=media&name={Uri.EscapeDataString(name)}", content))
            .EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static string[] Names(JsonElement list) =>
        list.TryGetProperty("items", out JsonElement items)
            ? items.EnumerateArray().Select(i => i.GetProperty("name").GetString()!).ToArray()
            : [];

    private static string[] Prefixes(JsonElement list) =>
        list.TryGetProperty("prefixes", out JsonElement prefixes)
            ? prefixes.EnumerateArray().Select(p => p.GetString()!).ToArray()
            : [];

    private static async Task SeedAsync(EmulatorFixture fx)
    {
        await CreateBucketAsync(fx, "b");
        foreach (string name in new[] { "a.txt", "b.txt", "photos/1.jpg", "photos/2.jpg", "photos/sub/3.jpg" })
        {
            await UploadAsync(fx, "b", name);
        }
    }

    [Fact]
    public async Task List_NoFilter_ReturnsEverything()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await SeedAsync(fx);

        JsonElement list = await ReadJsonAsync(await fx.Client.GetAsync("/storage/v1/b/b/o"));

        Assert.Equal("storage#objects", list.GetProperty("kind").GetString());
        Assert.Equal(5, Names(list).Length);
    }

    [Fact]
    public async Task List_WithDelimiter_RollsUpPrefixes()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await SeedAsync(fx);

        JsonElement list = await ReadJsonAsync(await fx.Client.GetAsync("/storage/v1/b/b/o?delimiter=/"));

        Assert.Equal(new[] { "a.txt", "b.txt" }, Names(list));
        Assert.Equal(new[] { "photos/" }, Prefixes(list));
    }

    [Fact]
    public async Task List_WithPrefixAndDelimiter_ScopesToSubdir()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await SeedAsync(fx);

        JsonElement list = await ReadJsonAsync(
            await fx.Client.GetAsync("/storage/v1/b/b/o?prefix=photos/&delimiter=/"));

        Assert.Equal(new[] { "photos/1.jpg", "photos/2.jpg" }, Names(list));
        Assert.Equal(new[] { "photos/sub/" }, Prefixes(list));
    }

    [Fact]
    public async Task List_WithPrefixOnly_FiltersByPrefix()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await SeedAsync(fx);

        JsonElement list = await ReadJsonAsync(await fx.Client.GetAsync("/storage/v1/b/b/o?prefix=photos/"));

        Assert.Equal(new[] { "photos/1.jpg", "photos/2.jpg", "photos/sub/3.jpg" }, Names(list));
        Assert.Empty(Prefixes(list));
    }

    [Fact]
    public async Task Delete_RemovesObject()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await SeedAsync(fx);

        HttpResponseMessage del = await fx.Client.DeleteAsync("/storage/v1/b/b/o/a.txt");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        HttpResponseMessage get = await fx.Client.GetAsync("/storage/v1/b/b/o/a.txt");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        JsonElement list = await ReadJsonAsync(await fx.Client.GetAsync("/storage/v1/b/b/o"));
        Assert.DoesNotContain("a.txt", Names(list));
    }

    [Fact]
    public async Task Delete_Missing_Returns404WithGcpErrorShape()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await CreateBucketAsync(fx, "b");

        HttpResponseMessage del = await fx.Client.DeleteAsync("/storage/v1/b/b/o/ghost.txt");

        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
        JsonElement err = await ReadJsonAsync(del);
        JsonElement error = err.GetProperty("error");
        Assert.Equal(404, error.GetProperty("code").GetInt32());
        JsonElement first = error.GetProperty("errors")[0];
        Assert.Equal("global", first.GetProperty("domain").GetString());
        Assert.Equal("notFound", first.GetProperty("reason").GetString());
        Assert.False(string.IsNullOrEmpty(error.GetProperty("message").GetString()));
    }
}
