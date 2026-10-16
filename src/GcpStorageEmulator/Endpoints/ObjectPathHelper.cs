using Microsoft.AspNetCore.Http.Features;

namespace GcpStorageEmulator.Endpoints;

/// <summary>
/// Extracts object names from request paths. GCS object names routinely contain '/', sent
/// percent-encoded as <c>%2F</c>. Rather than depend on how the server decodes the path for
/// routing, we read the untouched <see cref="IHttpRequestFeature.RawTarget"/> and decode the
/// relevant portion ourselves, which is deterministic.
/// </summary>
public static class ObjectPathHelper
{
    /// <summary>For JSON-API paths of the form <c>.../b/{bucket}/o/{name}</c>.</summary>
    public static string ExtractObjectName(HttpContext context, string bucket)
    {
        string raw = RawPath(context);
        string marker = $"/b/{bucket}/o/";
        int idx = raw.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return string.Empty;
        }

        return Uri.UnescapeDataString(raw[(idx + marker.Length)..]);
    }

    /// <summary>For path-style URLs of the form <c>/{bucket}/{name}</c> (signed downloads).</summary>
    public static string ExtractPathStyleObjectName(HttpContext context)
    {
        string raw = RawPath(context);
        string trimmed = raw.StartsWith('/') ? raw[1..] : raw;
        int slash = trimmed.IndexOf('/');
        return slash < 0 ? string.Empty : Uri.UnescapeDataString(trimmed[(slash + 1)..]);
    }

    private static string RawPath(HttpContext context)
    {
        string raw = context.Features.Get<IHttpRequestFeature>()?.RawTarget
                     ?? context.Request.Path.Value
                     ?? string.Empty;

        int query = raw.IndexOf('?');
        return query >= 0 ? raw[..query] : raw;
    }
}
