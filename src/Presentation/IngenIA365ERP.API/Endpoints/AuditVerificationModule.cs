using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Audit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T090 — Verificador SaaS-global del HMAC de un PDF exportado por T089.
///
/// <para>
/// El cliente sube el PDF + headers <c>X-Audit-Hmac</c> y
/// <c>X-Audit-Key-Version</c> (los mismos que el export devolvió), y el
/// endpoint recalcula con la clave correspondiente y compara en tiempo
/// constante. Útil para auditores externos que quieren confirmar que un PDF
/// archivado no fue alterado y que la firma corresponde a una clave aún
/// vigente.
/// </para>
///
/// <para>
/// El endpoint es operativo (solo operadores SaaS) — exige el permiso
/// <c>Saas.AuditLog.Verify</c>. No registra evento de auditoría porque
/// es una operación de solo lectura sin datos sensibles del tenant.
/// </para>
/// </summary>
public sealed class AuditVerificationModule : ICarterModule
{
    private const long MaxPdfBytes = 50 * 1024 * 1024; // 50 MB — defensa contra abuso

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/saas/audit")
            .WithTags("Audit / SaaS")
            .RequireAuthorization();

        group.MapPost("/verify", VerifyAsync)
            .WithName("Audit_VerifyPdfSignature")
            .DisableAntiforgery() // multipart sin form anti-XSRF — endpoint API
            .RequirePermission("Saas.AuditLog.Verify");
    }

    private static async Task<IResult> VerifyAsync(
        [FromForm] IFormFile file,
        [FromHeader(Name = "X-Audit-Hmac")] string? providedHmac,
        [FromHeader(Name = "X-Audit-Key-Version")] string? keyVersion,
        IAuditSignatureService signatures,
        HttpContext http,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Failure(http, "Validation.Invalid", "No se recibió el PDF a verificar.");
        }

        if (file.Length > MaxPdfBytes)
        {
            return Failure(http, "Validation.Invalid",
                $"El archivo excede el tamaño máximo permitido ({MaxPdfBytes / (1024 * 1024)} MB).");
        }

        if (string.IsNullOrWhiteSpace(providedHmac) || string.IsNullOrWhiteSpace(keyVersion))
        {
            return Failure(http, "Validation.Invalid",
                "Faltan headers X-Audit-Hmac o X-Audit-Key-Version.");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var computed = signatures.ComputeHmacBase64(bytes);
        var valid = signatures.VerifyHmacBase64(bytes, providedHmac, keyVersion);

        return Results.Ok(new
        {
            valid,
            keyVersion,
            computedHmac = computed,
            providedHmac
        });
    }

    private static IResult Failure(HttpContext http, string code, string message) =>
        Results.Json(
            new { code, message, traceId = http.TraceIdentifier },
            statusCode: code.StartsWith("Validation.", StringComparison.Ordinal)
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status422UnprocessableEntity);
}
