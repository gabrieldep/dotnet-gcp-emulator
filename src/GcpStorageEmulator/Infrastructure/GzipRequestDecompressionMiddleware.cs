using System.IO.Compression;

namespace GcpStorageEmulator.Infrastructure;

/// <summary>
/// Transparently decompresses request bodies sent with <c>Content-Encoding: gzip</c>. The
/// Google client libraries gzip JSON request bodies by default (GZipEnabled), and the real GCS
/// endpoint decompresses them server-side, so the emulator must too. Media/upload bytes are sent
/// without this header and pass through untouched.
/// </summary>
public sealed class GzipRequestDecompressionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string encoding = context.Request.Headers.ContentEncoding.ToString();
        if (encoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Body = new GZipStream(context.Request.Body, CompressionMode.Decompress);
            // The declared length/encoding no longer describe the stream the handlers will read.
            context.Request.Headers.Remove("Content-Encoding");
            context.Request.Headers.ContentLength = null;
        }

        await next(context);
    }
}
