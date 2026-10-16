using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GcpStorageEmulator.Tests.Support;

namespace GcpStorageEmulator.Tests;

public class BucketApiTests
{
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task CreateBucket_ReturnsGcpShape_AndCreatesFolder()
    {
        await using var fx = await EmulatorFixture.StartAsync();

        HttpResponseMessage response = await fx.Client.PostAsJsonAsync(
            "/storage/v1/b?project=demo", new { name = "my-bucket" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement json = await ReadJsonAsync(response);
        Assert.Equal("storage#bucket", json.GetProperty("kind").GetString());
        Assert.Equal("my-bucket", json.GetProperty("name").GetString());
        Assert.Equal("my-bucket", json.GetProperty("id").GetString());
        Assert.Equal("1", json.GetProperty("metageneration").GetString());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("timeCreated").GetString()));
        Assert.EndsWith("/storage/v1/b/my-bucket", json.GetProperty("selfLink").GetString());

        Assert.True(Directory.Exists(Path.Combine(fx.StorageRoot, "my-bucket")));
    }

    [Fact]
    public async Task CreateBucket_Duplicate_Returns409()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await fx.Client.PostAsJsonAsync("/storage/v1/b?project=demo", new { name = "dup" });

        HttpResponseMessage response = await fx.Client.PostAsJsonAsync(
            "/storage/v1/b?project=demo", new { name = "dup" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        JsonElement json = await ReadJsonAsync(response);
        Assert.Equal(409, json.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task CreateBucket_WithoutName_Returns400()
    {
        await using var fx = await EmulatorFixture.StartAsync();

        HttpResponseMessage response = await fx.Client.PostAsJsonAsync(
            "/storage/v1/b?project=demo", new { location = "US" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBucket_ReturnsResource_AndMissingReturns404()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await fx.Client.PostAsJsonAsync("/storage/v1/b?project=demo", new { name = "b1" });

        JsonElement got = await ReadJsonAsync(await fx.Client.GetAsync("/storage/v1/b/b1"));
        Assert.Equal("b1", got.GetProperty("name").GetString());

        HttpResponseMessage missing = await fx.Client.GetAsync("/storage/v1/b/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        JsonElement err = await ReadJsonAsync(missing);
        Assert.Equal("notFound", err.GetProperty("error").GetProperty("errors")[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ListBuckets_ReturnsAllCreated()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await fx.Client.PostAsJsonAsync("/storage/v1/b?project=demo", new { name = "aaa" });
        await fx.Client.PostAsJsonAsync("/storage/v1/b?project=demo", new { name = "bbb" });

        JsonElement json = await ReadJsonAsync(await fx.Client.GetAsync("/storage/v1/b?project=demo"));

        Assert.Equal("storage#buckets", json.GetProperty("kind").GetString());
        string[] names = json.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("name").GetString()!)
            .ToArray();
        Assert.Contains("aaa", names);
        Assert.Contains("bbb", names);
    }

    [Fact]
    public async Task DeleteBucket_RemovesIt()
    {
        await using var fx = await EmulatorFixture.StartAsync();
        await fx.Client.PostAsJsonAsync("/storage/v1/b?project=demo", new { name = "temp" });

        HttpResponseMessage del = await fx.Client.DeleteAsync("/storage/v1/b/temp");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        HttpResponseMessage get = await fx.Client.GetAsync("/storage/v1/b/temp");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        HttpResponseMessage delMissing = await fx.Client.DeleteAsync("/storage/v1/b/temp");
        Assert.Equal(HttpStatusCode.NotFound, delMissing.StatusCode);
    }
}
