namespace GcpStorageEmulator.Endpoints;

/// <summary>
/// The emulator serves its API under several path prefixes at once.
/// <para>
/// The official Google SDK, when <c>STORAGE_EMULATOR_HOST</c> is set, talks to the emulator at
/// the host <b>root</b> (e.g. <c>POST /b?project=...</c>) rather than under the canonical
/// <c>/storage/v1</c> path used by the public endpoint and by curl. We register both so the same
/// application code works against either, matching fake-gcs-server's behaviour.
/// </para>
/// </summary>
public static class RoutePrefixes
{
    public static readonly string[] Json = ["/storage/v1", string.Empty];

    public static readonly string[] Upload = ["/upload/storage/v1", "/upload"];

    public static readonly string[] Download = ["/download/storage/v1", "/download"];
}
