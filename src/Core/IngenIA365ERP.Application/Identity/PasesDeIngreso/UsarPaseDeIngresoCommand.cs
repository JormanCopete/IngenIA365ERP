using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Auth.Common;
using IngenIA365ERP.Application.Identity.Auth.Login;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.PasesDeIngreso;

/// <summary>
/// Feature 027 (HU1, HU2): canjea un pase de ingreso en el desafío del segundo factor. Se llama con el token <c>purpose=mfa-verify</c>, es
/// decir, con la contraseña ya acertada en el login de siempre: el pase reemplaza sólo el código de la app (FR-005, FR-006).
/// </summary>
/// <param name="Pass">El valor <c>t</c> del enlace, o el enlace completo pegado (en MAUI): se extrae <c>t</c>.</param>
public sealed record UsarPaseDeIngresoCommand(string? Pass, string? IpAddress = null, string? UserAgent = null)
    : IRequest<Result<LoginResult>>;

public sealed class UsarPaseDeIngresoCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext admin,
    ITenantMembershipReader memberships,
    ISecureTokenGenerator tokens,
    IEmisorDeSesionTrasSegundoFactor emisorDeSesion,
    IAvisosDelPaseDeIngreso avisos,
    IAuditAppendOnlyWriter auditWriter,
    ILoginAttemptCounter contadorDeIntentos,
    IDateTimeService clock,
    ILogger<UsarPaseDeIngresoCommandHandler> logger)
    : IRequestHandler<UsarPaseDeIngresoCommand, Result<LoginResult>>
{
    /// <summary>Un solo mensaje para inexistente, vencido, anulado o de otra cuenta (FR-018): no se revela nada de la cuenta.</summary>
    private const string MensajeGenerico =
        "El pase de ingreso no es válido o ya venció. Entra con tu app o pide otro pase a tu administrador.";

    public async Task<Result<LoginResult>> Handle(UsarPaseDeIngresoCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return Result.Failure<LoginResult>("Identity.Unauthenticated", "Falta el token de challenge.");
        if (!string.Equals(currentUser.Purpose, CentralJwtPurposes.MfaVerify, StringComparison.Ordinal))
            return Result.Failure<LoginResult>("Identity.WrongTokenPurpose",
                $"Este endpoint requiere purpose=mfa-verify, recibido '{currentUser.Purpose}'.");

        var usuario = await centralIdentity.FindByIdAsync(currentUser.CentralUserId.Value, ct);
        if (usuario is null)
            return Result.Failure<LoginResult>("Identity.Unauthenticated", "Usuario no encontrado.");

        // El mismo contador del segundo factor: probar pases al azar cuesta igual que probar códigos.
        var correo = usuario.Email ?? string.Empty;
        var bloqueo = await contadorDeIntentos.CheckAsync(AmbitoDeIntentos.Mfa, correo, ct);
        if (bloqueo.IsLocked)
            return Result.Failure<LoginResult>("Identity.Locked.Soft",
                $"Demasiados intentos con el segundo factor. Reintenta en {bloqueo.RetryAfterSeconds} segundos.");

        var ahora = clock.UtcNow;
        var pase = await BuscarAsync(request.Pass, ct);

        if (pase is null || pase.CentralUserId != usuario.Id || !pase.EstaVigente(ahora))
            return await FallarAsync(usuario, pase?.CentralUserId == usuario.Id ? pase : null, "invalido", request, ahora, ct);

        // Lo que pudo cambiar desde la concesión se comprueba ahora (research R5): si ya no vale, se anula con su motivo.
        var motivoDeAnulacion = await MotivoParaAnularAsync(pase, usuario, ct);
        if (motivoDeAnulacion is not null)
        {
            pase.Anular(ahora, motivoDeAnulacion, null);
            await admin.SaveChangesAsync(ct);
            return await FallarAsync(usuario, pase, motivoDeAnulacion, request, ahora, ct);
        }

        var resultado = await emisorDeSesion.EmitirConPaseAsync(usuario, pase.TenantId, ct);
        if (resultado.IsFailure || resultado.Value.Challenge == LoginChallenges.NoActiveMembership)
            return resultado.IsFailure ? resultado : await FallarAsync(usuario, pase, "sin-membresia", request, ahora, ct);

        pase.RegistrarUso(ahora, request.IpAddress);
        await admin.SaveChangesAsync(ct);
        await contadorDeIntentos.ResetAsync(AmbitoDeIntentos.Mfa, correo, ct);

        await AuditarAsync(usuario, pase, AuditEventTypes.LoginPassUsed, 200, request, ahora, ct);
        try
        {
            var nombre = await admin.Tenants.AsNoTracking().Where(t => t.PublicId == pase.TenantId).Select(t => t.Name).FirstOrDefaultAsync(ct);
            await avisos.AvisarUsoAsync(correo, nombre ?? "tu cooperativa", ahora, request.IpAddress, request.UserAgent, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo avisar a {Correo} del ingreso con el pase {Pase}.", correo, pase.PublicId);
        }

        return resultado;
    }

    private async Task<LoginPass?> BuscarAsync(string? valor, CancellationToken ct)
    {
        var token = ExtraerToken(valor);
        if (token is null) return null;
        byte[] huella;
        try
        {
            huella = tokens.HashPlainToken(token);
        }
        catch (FormatException)
        {
            return null;
        }
        return await admin.LoginPasses.FirstOrDefaultAsync(p => p.TokenHash == huella, ct);
    }

    /// <summary>Acepta el valor de <c>t</c> o el enlace completo (lo que se pega en MAUI).</summary>
    public static string? ExtraerToken(string? valor)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto)) return null;
        var i = texto.IndexOf("t=", StringComparison.Ordinal);
        if (i >= 0 && (i == 0 || texto[i - 1] is '?' or '&'))
        {
            texto = texto[(i + 2)..];
            var fin = texto.IndexOf('&');
            if (fin >= 0) texto = texto[..fin];
            texto = Uri.UnescapeDataString(texto);
        }
        return texto.Length is >= 16 and <= 200 ? texto : null;
    }

    private async Task<string?> MotivoParaAnularAsync(LoginPass pase, CentralUser usuario, CancellationToken ct)
    {
        if (usuario.Status != CentralUserStatus.Active || usuario.IsDeleted) return LoginPass.Motivos.CuentaInactiva;
        if (!string.Equals(pase.SecurityStampAtGrant, usuario.SecurityStamp, StringComparison.Ordinal))
            return LoginPass.Motivos.CambioDeCredenciales;

        var politica = await admin.TenantMfaPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.TenantId == pase.TenantId, ct);
        if (politica?.AllowLoginPass != true) return LoginPass.Motivos.OpcionApagada;

        var activas = await memberships.GetActiveMembershipsAsync(usuario.Id, ct);
        return activas.Any(m => m.TenantId == pase.TenantId) ? null : LoginPass.Motivos.SinMembresia;
    }

    private async Task<Result<LoginResult>> FallarAsync(CentralUser usuario, LoginPass? pase, string detalle,
        UsarPaseDeIngresoCommand request, DateTime ahora, CancellationToken ct)
    {
        await contadorDeIntentos.RecordFailureAsync(AmbitoDeIntentos.Mfa, usuario.Email ?? string.Empty, ct);
        await AuditarAsync(usuario, pase, AuditEventTypes.LoginPassUseFailed, 401, request, ahora, ct, detalle);
        return Result.Failure<LoginResult>("Identity.LoginPass.Invalid", MensajeGenerico);
    }

    private async Task AuditarAsync(CentralUser usuario, LoginPass? pase, string accion, int estado, UsarPaseDeIngresoCommand request,
        DateTime ahora, CancellationToken ct, string? detalle = null)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: pase?.TenantId.ToString("N") ?? string.Empty,
                UserId: usuario.Id.ToString("N"),
                UserName: usuario.Email,
                Action: accion,
                EntityType: nameof(LoginPass),
                EntityPublicId: pase?.PublicId.ToString("N") ?? usuario.Id.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: detalle is null ? null : System.Text.Json.JsonSerializer.Serialize(new { detalle }),
                ChangedFields: null,
                IpAddress: request.IpAddress,
                UserAgent: request.UserAgent,
                Endpoint: "/api/auth/mfa/pass",
                HttpMethod: "POST",
                HttpStatusCode: estado,
                DurationMs: null,
                OccurredAt: ahora), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Accion}", accion);
        }
    }
}
