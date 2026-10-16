namespace GcpStorageEmulator.Configuration;

/// <summary>
/// Runtime configuration for the emulator. Bound from the "Emulator" configuration
/// section and/or environment variables (e.g. <c>Emulator__StorageRoot</c>).
/// </summary>
public sealed class EmulatorOptions
{
    public const string SectionName = "Emulator";

    /// <summary>
    /// Root directory on the local filesystem where buckets and objects are persisted.
    /// Relative paths are resolved against the process content root.
    /// </summary>
    public string StorageRoot { get; set; } = "./.gcs-data";

    /// <summary>
    /// TCP port the HTTP listener binds to. Also configurable through the standard
    /// ASPNETCORE_URLS mechanism; when set here it takes precedence.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Optional absolute base URL (scheme://host[:port]) advertised in generated
    /// resource links and resumable-upload session URIs. When empty the value is
    /// derived from the incoming request, which is the most robust default.
    /// </summary>
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// Default project id reported for buckets that do not carry one.
    /// </summary>
    public string DefaultProjectId { get; set; } = "emulator-project";

    /// <summary>
    /// When true every incoming request is logged (method, raw target and the headers
    /// relevant to the upload protocol). Handy when reverse-engineering SDK behaviour.
    /// </summary>
    public bool LogRequests { get; set; }
}
