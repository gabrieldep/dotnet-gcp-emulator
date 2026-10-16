using GcpStorageEmulator;
using GcpStorageEmulator.Configuration;

var builder = WebApplication.CreateBuilder(args);
EmulatorApp.ConfigureServices(builder);

// The Emulator:Port option takes precedence over ASPNETCORE_URLS when provided.
int? port = builder.Configuration.GetValue<int?>($"{EmulatorOptions.SectionName}:Port");
if (port is > 0)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

WebApplication app = builder.Build();
EmulatorApp.Configure(app);
app.Run();

/// <summary>Entry-point marker so integration tests can reference the host.</summary>
public partial class Program;
