using Microsoft.AspNetCore.Http.Features;

namespace GcpStorageEmulator.Infrastructure;

/// <summary>
/// Logs every request's method and raw target together with the headers that matter for the
/// GCS upload protocol. Enabled via <c>Emulator:LogRequests</c>; invaluable when matching the
/// behaviour the official SDK expects.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string? rawTarget = context.Features.Get<IHttpRequestFeature>()?.RawTarget
                            ?? context.Request.Path.Value;

        logger.LogInformation(
            "--> {Method} {RawTarget} | Content-Type={ContentType} Content-Length={ContentLength} Content-Encoding={ContentEncoding} Content-Range={ContentRange}",
            context.Request.Method,
            rawTarget,
            context.Request.ContentType,
            context.Request.ContentLength,
            context.Request.Headers.ContentEncoding.ToString(),
            context.Request.Headers.ContentRange.ToString());

        await next(context);

        logger.LogInformation(
            "<-- {StatusCode} {Method} {RawTarget} | Location={Location} Range={Range}",
            context.Response.StatusCode,
            context.Request.Method,
            rawTarget,
            context.Response.Headers.Location.ToString(),
            context.Response.Headers["Range"].ToString());
    }
}
