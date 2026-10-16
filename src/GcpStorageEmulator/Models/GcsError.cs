using System.Text.Json.Serialization;

namespace GcpStorageEmulator.Models;

/// <summary>Top-level envelope for the GCS JSON error shape.</summary>
public sealed class GcsErrorResponse
{
    [JsonPropertyName("error")]
    public required GcsError Error { get; init; }
}

public sealed class GcsError
{
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("errors")]
    public required IReadOnlyList<GcsErrorItem> Errors { get; init; }
}

public sealed class GcsErrorItem
{
    [JsonPropertyName("domain")]
    public string Domain { get; init; } = "global";

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
