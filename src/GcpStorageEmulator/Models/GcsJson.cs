using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GcpStorageEmulator.Models;

/// <summary>Shared JSON settings and formatting helpers for GCS-shaped payloads.</summary>
public static class GcsJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Formats a timestamp the way the GCS JSON API does (RFC3339, UTC, 'Z').</summary>
    public static string Rfc3339(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
