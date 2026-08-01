using System.Text.Json;

namespace IngenIA365ERP.API.Middleware.CentralIdentity;

/// <summary>
/// Traduce respuestas <c>401 Unauthorized</c> sin body al envelope JSON
/// estándar del proyecto (T045): <c>{ "errorCode": "Identity.Unauthenticated",
/// "message": "..." }</c>. Mantiene consistencia con
/// <c>ErrorEnvelopeFilter</c> que usa <c>Result&lt;T&gt;</c>.
///
/// <para>Se monta DESPUÉS del <c>UseAuthentication()</c> y ANTES del
/// <c>UseAuthorization()</c> en Program.cs. Para no romper Fase 0, el wiring
/// queda diferido a Chunk D.</para>
/// </summary>
public sealed class CentralIdentityChallengeMiddleware
{
    private readonly RequestDelegate _next;

    public CentralIdentityChallengeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Solo reformatea respuestas 401 SIN body que provienen del challenge default.
        if (context.Response.StatusCode != StatusCodes.Status401Unauthorized) return;
        if (context.Response.HasStarted) return;
        if (context.Response.ContentLength is > 0) return;

        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            errorCode = "Identity.Unauthenticated",
            message = "Se requiere autenticación válida.",
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

public static class CentralIdentityChallengeMiddlewareExtensions
{
    public static IApplicationBuilder UseCentralIdentityChallenge(this IApplicationBuilder app)
        => app.UseMiddleware<CentralIdentityChallengeMiddleware>();
}
