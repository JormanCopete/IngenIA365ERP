using IngenIA365ERP.Application.Common.Models;
using Microsoft.AspNetCore.Http;

namespace IngenIA365ERP.API.Filters;

/// <summary>
/// Endpoint filter que traduce <see cref="Result"/>/<see cref="Result{T}"/>
/// devueltos por los handlers MediatR a una respuesta JSON con la envolvente
/// estándar <c>{ code, message, traceId }</c> (FR-048).
///
/// El status HTTP se infiere del prefijo del <c>Error.Code</c>:
///  * <c>Validation.*</c> → 400
///  * <c>*.NotFound</c>   → 404
///  * <c>*.Unauthorized</c> → 401
///  * <c>*.Forbidden</c>  → 403
///  * <c>Concurrency.*</c> → 409
///  * resto              → 422 (regla de negocio incumplida)
///
/// Para <see cref="Result{T}.Success"/>, se devuelve <c>Value</c> con 200.
/// Para <see cref="Result.Success"/> sin valor, se devuelve 204.
/// </summary>
public sealed class ErrorEnvelopeFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        return Translate(context.HttpContext, result);
    }

    /// <summary>
    /// Punto de extensión público para endpoints que necesitan custom IResult
    /// en el camino feliz (descarga binaria con headers, redirects, etc.) pero
    /// quieren reusar el mismo envelope canónico para fallas.
    /// </summary>
    public static object? Translate(HttpContext http, object? result)
    {
        if (result is null) return Results.NoContent();

        if (result is Result r)
        {
            if (r.IsSuccess)
            {
                // Result<T> exposes Value via reflection
                var valueProp = result.GetType().GetProperty("Value");
                if (valueProp is not null)
                {
                    var value = valueProp.GetValue(result);
                    return value is null ? Results.NoContent() : Results.Ok(value);
                }
                return Results.NoContent();
            }

            var (status, code, message) = MapFailure(r.Error);
            var traceId = http.TraceIdentifier;
            return Results.Json(
                new { code, message, traceId },
                statusCode: status);
        }

        // Pass-through para handlers que no devuelven Result (ej. IResult ya construido).
        return result;
    }

    private static (int Status, string Code, string Message) MapFailure(Error error)
    {
        var code = string.IsNullOrEmpty(error.Code) ? "Generic.Failure" : error.Code;
        var status = code switch
        {
            _ when code.StartsWith("Validation.", StringComparison.Ordinal) => StatusCodes.Status400BadRequest,
            _ when code.EndsWith(".NotFound", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
            _ when code.EndsWith(".Unauthorized", StringComparison.Ordinal) => StatusCodes.Status401Unauthorized,
            _ when code.EndsWith(".Forbidden", StringComparison.Ordinal) => StatusCodes.Status403Forbidden,
            _ when code.StartsWith("Concurrency.", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
            _ when code.EndsWith(".Conflict", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        return (status, code, error.Message);
    }
}
