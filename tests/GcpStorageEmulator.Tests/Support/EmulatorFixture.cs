using GcpStorageEmulator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GcpStorageEmulator.Tests.Support;

/// <summary>
/// Boots a real instance of the emulator on an ephemeral Kestrel port backed by a throwaway
/// storage directory. Using a real socket (rather than the in-memory TestServer) keeps the
/// behaviour faithful, which matters for raw-target/%2F handling and for the official SDK.
/// </summary>
public sealed class EmulatorFixture : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly TempDirectory _dir;

    private EmulatorFixture(WebApplication app, TempDirectory dir, string baseAddress)
    {
        _app = app;
        _dir = dir;
        BaseAddress = baseAddress;
        Client = new HttpClient { BaseAddress = new Uri(baseAddress) };
    }

    /// <summary>Absolute base address, e.g. <c>http://127.0.0.1:49512</c>.</summary>
    public string BaseAddress { get; }

    /// <summary>Host:port portion, suitable for <c>STORAGE_EMULATOR_HOST</c>.</summary>
    public string Host => new Uri(BaseAddress).Authority;

    public HttpClient Client { get; }

    public string StorageRoot => _dir.Path;

    public static async Task<EmulatorFixture> StartAsync(Action<Dictionary<string, string?>>? configure = null)
    {
        var dir = new TempDirectory();
        var settings = new Dictionary<string, string?>
        {
            ["Emulator:StorageRoot"] = dir.Path,
            ["Emulator:LogRequests"] = "false",
        };
        configure?.Invoke(settings);

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Logging.ClearProviders();
        EmulatorApp.ConfigureServices(builder);
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        WebApplication app = builder.Build();
        EmulatorApp.Configure(app);
        await app.StartAsync();

        string address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .First();

        return new EmulatorFixture(app, dir, address);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        _dir.Dispose();
    }
}
