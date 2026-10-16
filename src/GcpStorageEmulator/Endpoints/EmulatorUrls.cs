using GcpStorageEmulator.Configuration;

namespace GcpStorageEmulator.Endpoints;

/// <summary>Resolves the externally reachable base URL used in generated links and session URIs.</summary>
public static class EmulatorUrls
{
    public static string BaseUrl(HttpContext context, EmulatorOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ExternalUrl))
        {
            return options.ExternalUrl!.TrimEnd('/');
        }

        HttpRequest request = context.Request;
        return $"{request.Scheme}://{request.Host.Value}";
    }
}
