using GcpStorageEmulator.Models;

namespace GcpStorageEmulator.Endpoints;

/// <summary>Helpers producing GCS-shaped JSON results.</summary>
public static class GcsResults
{
    public static IResult Json<T>(T value, int statusCode = StatusCodes.Status200OK) =>
        Results.Json(value, GcsJson.Options, contentType: "application/json; charset=UTF-8", statusCode: statusCode);

    public static IResult Error(int code, string reason, string message) =>
        Results.Json(
            new GcsErrorResponse
            {
                Error = new GcsError
                {
                    Code = code,
                    Message = message,
                    Errors = [new GcsErrorItem { Reason = reason, Message = message }],
                },
            },
            GcsJson.Options,
            contentType: "application/json; charset=UTF-8",
            statusCode: code);

    public static IResult NotFound(string message) => Error(StatusCodes.Status404NotFound, "notFound", message);

    public static IResult BadRequest(string reason, string message) => Error(StatusCodes.Status400BadRequest, reason, message);
}
