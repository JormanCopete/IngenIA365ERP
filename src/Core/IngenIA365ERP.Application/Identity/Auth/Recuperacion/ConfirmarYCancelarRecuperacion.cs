using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Auth.Recuperacion;

/// <summary>
/// Ejecuta la recuperación: retira TODOS los autenticadores y rota el sello de
/// seguridad.
///
/// <para>
/// <b>Pide la contraseña otra vez, y aquí y no en la solicitud.</b> Entre pedirla
/// y confirmarla pasan horas o días, y en ese tiempo el enlace puede acabar en
/// otras manos —un buzón compartido, un reenvío, un portátil abierto—. Pedirla
/// sólo al principio dejaría que quien encontrara el correo terminara el trabajo.
/// </para>
///
/// <para>
/// <b>No emite sesión.</b> Recuperar no es entrar: es poder volver a inscribir un
/// segundo factor. Quien termina esto vuelve al login, escribe su contraseña, y el
/// sistema le pide inscribir. Devolver una sesión aquí convertiría «tengo el buzón
/// y la contraseña» en «estoy dentro», que es justo lo que el segundo factor
/// existe para impedir.
/// </para>
/// </summary>
public sealed record ConfirmMfaRecoveryCommand(
    string Token, string Password, string? IpAddress = null, string? UserAgent = null)
    : IRequest<Result>;

public sealed class ConfirmMfaRecoveryCommandHandler(
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext adminDb,
    ISecureTokenGenerator tokens,
    ILoginAttemptCounter contadorDeIntentos,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<ConfirmMfaRecoveryCommandHandler> logger)
    : IRequestHandler<ConfirmMfaRecoveryCommand, Result>
{
    public async Task<Result> Handle(ConfirmMfaRecoveryCommand request, CancellationToken ct)
    {
        var ahora = clock.UtcNow;

        var solicitud = await BuscarPorTokenAsync(
            adminDb, tokens, request.Token, porCancelacion: false, ct);

        // Mensaje único para «no existe», «ya se usó», «caducó» y «se canceló». Un
        // enlace ajeno no puede servir para averiguar si existía.
        if (solicitud is null || !solicitud.EstaViva(ahora))
        {
            return Result.Failure(
                "Identity.RecuperacionNoValida",
                "Ese enlace ya no sirve. Volvé a pedir la recuperación desde el ingreso.");
        }

        if (!solicitud.SePuedeEjecutar(ahora))
        {
            // Este sí es un mensaje distinto, y tiene que serlo: no es un error, es
            // que todavía no toca. Confundirlo con «no sirve» haría que la persona
            // volviera a pedirla, reiniciando el reloj cada vez.
            return Result.Failure(
                "Identity.RecuperacionTodaviaNoDisponible",
                $"Esta recuperación se puede completar a partir de " +
                $"{solicitud.EjecutableDesde:yyyy-MM-dd HH:mm} UTC. " +
                "Hasta entonces podés cancelarla desde el mismo correo.");
        }

        var usuario = await centralIdentity.FindByIdAsync(solicitud.CentralUserId, ct);
        if (usuario is null)
        {
            return Result.Failure(
                "Identity.RecuperacionNoValida", "Ese enlace ya no sirve.");
        }

        var correo = usuario.Email ?? string.Empty;

        // Ámbito propio del contador. No el del segundo factor y no el de la
        // contraseña: el del segundo factor lo resetea un ingreso correcto, y este
        // endpoint es anónimo, así que compartirlo regalaría reintentos. Aquí se
        // adivina una contraseña con un enlace en la mano, y eso merece su propio
        // presupuesto.
        var bloqueo = await contadorDeIntentos.CheckAsync(
            AmbitoDeIntentos.RecuperacionMfa, correo, ct);
        if (bloqueo.IsLocked)
        {
            return Result.Failure(
                "Identity.Locked.Soft",
                $"Demasiados intentos. Reintentá en {bloqueo.RetryAfterSeconds} segundos.");
        }

        var claveOk = await centralIdentity.ValidatePasswordAsync(
            solicitud.CentralUserId, request.Password, ct);

        if (!claveOk)
        {
            await contadorDeIntentos.RecordFailureAsync(
                AmbitoDeIntentos.RecuperacionMfa, correo, ct);
            await EmitirAuditoriaAsync(
                usuario, AuditEventTypes.MfaRecoveryConfirmFailed,
                request.IpAddress, request.UserAgent, ahora, 401, ct);

            return Result.Failure(
                "Identity.InvalidCredentials", "La contraseña no es correcta.");
        }

        // Un solo sitio para las tres vías —códigos de respaldo, doble aprobación y
        // ésta—: ResetMfaAsync retira todas las credenciales y rota el sello, que
        // además invalida los refresh vivos. Repartirlo fue el error de la Fase 0.
        await centralIdentity.ResetMfaAsync(solicitud.CentralUserId, ct);

        solicitud.MarcarEjecutada(ahora);
        await adminDb.SaveChangesAsync(ct);
        await contadorDeIntentos.ResetAsync(AmbitoDeIntentos.RecuperacionMfa, correo, ct);

        logger.LogWarning(
            "Segundo factor recuperado por correo para {Correo}. Solicitud {Id}, pedida el {Cuando}.",
            correo, solicitud.PublicId, solicitud.CreatedAt);

        await EmitirAuditoriaAsync(
            usuario, AuditEventTypes.MfaRecoveryExecuted,
            request.IpAddress, request.UserAgent, ahora, 200, ct);

        return Result.Success();
    }

    private async Task EmitirAuditoriaAsync(
        CentralUser usuario, string accion, string? ip, string? userAgent,
        DateTime ahora, int estado, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: usuario.Id.ToString("N"),
                UserName: usuario.Email,
                Action: accion,
                EntityType: nameof(MfaRecoveryRequest),
                EntityPublicId: usuario.Id.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: ip,
                UserAgent: userAgent,
                Endpoint: "/api/auth/mfa/recovery/confirm",
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

    internal static async Task<MfaRecoveryRequest?> BuscarPorTokenAsync(
        IAdminDbContext adminDb,
        ISecureTokenGenerator tokens,
        string tokenPlano,
        bool porCancelacion,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tokenPlano)) return null;

        byte[] hash;
        try
        {
            hash = tokens.HashPlainToken(tokenPlano);
        }
        catch (FormatException)
        {
            // Un token que ni siquiera es base64url. No es un caso de error del
            // sistema: es alguien pegando cualquier cosa en la barra de direcciones.
            return null;
        }

        return porCancelacion
            ? await adminDb.MfaRecoveryRequests
                .FirstOrDefaultAsync(r => r.CancelTokenHash == hash, ct)
            : await adminDb.MfaRecoveryRequests
                .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
    }
}

/// <summary>
/// Cancela una recuperación en curso. <b>No pide contraseña, y es deliberado</b>:
/// cancelar tiene que ser más fácil que ejecutar. Si pidiera lo mismo, la persona
/// que recibe el aviso a las tres de la mañana no cancelaría, y esa es exactamente
/// la reacción que toda la demora existe para provocar.
///
/// <para>
/// Cancelar de más no hace daño —lo peor que pasa es que haya que volver a pedirla,
/// con la contraseña— mientras que ejecutar de más retira el segundo factor de
/// alguien. Los dos lados del error no pesan lo mismo.
/// </para>
/// </summary>
public sealed record CancelMfaRecoveryCommand(
    string Token, string? IpAddress = null, string? UserAgent = null)
    : IRequest<Result>;

public sealed class CancelMfaRecoveryCommandHandler(
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext adminDb,
    ISecureTokenGenerator tokens,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<CancelMfaRecoveryCommandHandler> logger)
    : IRequestHandler<CancelMfaRecoveryCommand, Result>
{
    public async Task<Result> Handle(CancelMfaRecoveryCommand request, CancellationToken ct)
    {
        var ahora = clock.UtcNow;

        var solicitud = await ConfirmMfaRecoveryCommandHandler.BuscarPorTokenAsync(
            adminDb, tokens, request.Token, porCancelacion: true, ct);

        if (solicitud is null)
        {
            return Result.Failure(
                "Identity.RecuperacionNoValida", "Ese enlace de cancelación no sirve.");
        }

        // Ya cancelada o ya ejecutada: se responde éxito igual. Quien pulsa cancelar
        // dos veces quiere lo mismo las dos, y un error en la segunda le haría creer
        // que la primera no funcionó.
        var yaEstaba = !solicitud.EstaViva(ahora);
        solicitud.Cancelar(ahora, MfaRecoveryRequest.Motivos.LaPersonaCancelo);
        await adminDb.SaveChangesAsync(ct);

        if (!yaEstaba)
        {
            var usuario = await centralIdentity.FindByIdAsync(solicitud.CentralUserId, ct);

            // Nivel warning: alguien pidió retirar un segundo factor y su dueño lo
            // paró. Puede ser un despiste, y puede ser un ataque en curso — que es
            // el motivo por el que la demora existe.
            logger.LogWarning(
                "Recuperación de segundo factor CANCELADA por su dueño. Solicitud {Id}, " +
                "pedida desde {Ip} el {Cuando}.",
                solicitud.PublicId, solicitud.IpSolicitante ?? "?", solicitud.CreatedAt);

            await EmitirAuditoriaAsync(solicitud, usuario?.Email, request, ahora, ct);
        }

        return Result.Success();
    }

    private async Task EmitirAuditoriaAsync(
        MfaRecoveryRequest solicitud, string? correo,
        CancelMfaRecoveryCommand request, DateTime ahora, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: solicitud.CentralUserId.ToString("N"),
                UserName: correo ?? string.Empty,
                Action: AuditEventTypes.MfaRecoveryCancelled,
                EntityType: nameof(MfaRecoveryRequest),
                EntityPublicId: solicitud.PublicId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: request.IpAddress,
                UserAgent: request.UserAgent,
                Endpoint: "/api/auth/mfa/recovery/cancel",
                HttpMethod: "POST",
                HttpStatusCode: 200,
                DurationMs: null,
                OccurredAt: ahora), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló al cancelar una recuperación.");
        }
    }
}
