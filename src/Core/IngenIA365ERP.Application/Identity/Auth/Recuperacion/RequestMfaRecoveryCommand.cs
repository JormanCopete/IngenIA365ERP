using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Auth.Recuperacion;

/// <summary>
/// Pide recuperar el segundo factor por correo. <b>Se llama desde el desafío</b>,
/// con el token de <c>purpose=mfa-verify</c> — es decir, la contraseña ya está
/// acertada.
///
/// <para>
/// Eso no es un detalle de implementación: hace que este endpoint <b>no sirva de
/// oráculo</b>. Anónimo, cualquiera podría averiguar qué correos tienen cuenta y,
/// peor, inundar el buzón de una víctima sin saber siquiera su contraseña.
/// </para>
/// </summary>
public sealed record RequestMfaRecoveryCommand(string? IpAddress = null, string? UserAgent = null)
    : IRequest<Result<RequestMfaRecoveryResult>>;

/// <param name="EjecutableDesde">
/// Cuándo podrá completarse. Se devuelve para que la pantalla lo diga: «esto se
/// puede terminar el jueves a las 9» es la diferencia entre una espera entendida
/// y una que parece un fallo.
/// </param>
public sealed record RequestMfaRecoveryResult(DateTime EjecutableDesde, bool CorreoEnviado);

public sealed class RequestMfaRecoveryCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    IAdminDbContext adminDb,
    ISecureTokenGenerator tokens,
    IMfaRecoveryEmailDispatcher correo,
    ITopeDeSolicitudesDeRecuperacion tope,
    IConfiguration configuracion,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<RequestMfaRecoveryCommandHandler> logger)
    : IRequestHandler<RequestMfaRecoveryCommand, Result<RequestMfaRecoveryResult>>
{
    /// <summary>
    /// Apagado por defecto para el maestro. Es la cuenta que gobierna todas las
    /// cooperativas y la única sin nadie que la rescate; abrirle una vía que
    /// depende de un buzón es exactamente el riesgo que esta etapa intenta acotar.
    /// </summary>
    public const string ClaveParaElMaestro = "Mfa:RecuperacionPorCorreoParaMaestro";

    public async Task<Result<RequestMfaRecoveryResult>> Handle(
        RequestMfaRecoveryCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<RequestMfaRecoveryResult>(
                "Identity.Unauthenticated", "Falta el token de challenge.");
        }

        if (!string.Equals(currentUser.Purpose, CentralJwtPurposes.MfaVerify, StringComparison.Ordinal))
        {
            return Result.Failure<RequestMfaRecoveryResult>(
                "Identity.WrongTokenPurpose",
                "Esta vía se pide desde el desafío del segundo factor, con la contraseña ya acertada.");
        }

        var centralUserId = currentUser.CentralUserId.Value;
        var ahora = clock.UtcNow;

        var usuario = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (usuario is null)
        {
            return Result.Failure<RequestMfaRecoveryResult>(
                "Identity.Unauthenticated", "Usuario no encontrado.");
        }

        if (usuario.IsGlobalMasterAdmin
            && !configuracion.GetValue<bool>(ClaveParaElMaestro))
        {
            return Result.Failure<RequestMfaRecoveryResult>(
                "Identity.RecuperacionNoDisponible",
                "El administrador maestro no puede recuperar su segundo factor por correo. " +
                "Usá un código de respaldo.");
        }

        var activas = await memberships.GetActiveMembershipsAsync(centralUserId, ct);
        var veredicto = GuardiaDeRecuperacionPorCorreo.Evaluar(activas);
        if (!veredicto.Permitida)
        {
            return Result.Failure<RequestMfaRecoveryResult>(
                "Identity.RecuperacionNoDisponible", veredicto.Motivo!);
        }

        // Tope diario, en su propia clave. NO se usa el contador de intentos: aquel
        // escala castigando fallos, y aquí no hay fallo ninguno — cada solicitud es
        // legítima por separado. Lo que hay que acotar es «cuántas al día», que es
        // otra pregunta, y sin tope quien tenga la contraseña inunda el buzón de la
        // víctima con avisos hasta que deje de leerlos.
        var dentroDelTope = await tope.RegistrarYComprobarAsync(centralUserId, ct);
        if (!dentroDelTope)
        {
            return Result.Failure<RequestMfaRecoveryResult>(
                "Identity.DemasiadasSolicitudes",
                "Ya pediste esta recuperación varias veces hoy. Probá mañana, " +
                "o usá un código de respaldo.");
        }

        // Una sola solicitud viva por persona. La anterior se cancela en vez de
        // dejar dos enlaces activos: con dos, cancelar uno da la sensación de haber
        // parado algo que sigue en marcha.
        var vivas = await adminDb.MfaRecoveryRequests
            .Where(r => r.CentralUserId == centralUserId
                     && r.EjecutadaEn == null && r.CanceladaEn == null)
            .ToListAsync(ct);

        foreach (var anterior in vivas)
        {
            anterior.Cancelar(ahora, MfaRecoveryRequest.Motivos.Reemplazada);
        }

        var (tokenPlano, tokenHash) = tokens.Generate();
        var (cancelPlano, cancelHash) = tokens.Generate();

        var solicitud = MfaRecoveryRequest.Crear(
            centralUserId: centralUserId,
            tokenHash: tokenHash,
            cancelTokenHash: cancelHash,
            ahora: ahora,
            demora: veredicto.Demora,
            vigencia: GuardiaDeRecuperacionPorCorreo.VigenciaDelEnlace,
            ipSolicitante: request.IpAddress);

        adminDb.MfaRecoveryRequests.Add(solicitud);
        await adminDb.SaveChangesAsync(ct);

        // El correo se manda por IEmailSender directo, no por SendNotificationCommand:
        // aquel escribe en la base de UNA cooperativa, y aquí todavía no hay ninguna
        // elegida — el Principio IV exige que ese contexto lance en ese estado.
        var enviado = true;
        try
        {
            await correo.DespacharAsync(
                new AvisoDeRecuperacionMfa(
                    Correo: usuario.Email,
                    TokenDeConfirmacion: tokenPlano,
                    TokenDeCancelacion: cancelPlano,
                    EjecutableDesde: solicitud.EjecutableDesde,
                    ExpiraEn: solicitud.ExpiraEn,
                    IpSolicitante: request.IpAddress,
                    UserAgent: request.UserAgent),
                ct);
        }
        catch (Exception ex)
        {
            // Y si no salió, se dice. Responder «te enviamos un correo» cuando el
            // SMTP falló deja a la persona esperando un mensaje que no existe, con
            // la solicitud creada y su reloj corriendo. El SMTP es autoalojado: esto
            // pasa.
            enviado = false;
            logger.LogError(ex,
                "No se pudo enviar el aviso de recuperación de segundo factor a {Correo}. " +
                "La solicitud {Id} quedó creada.", usuario.Email, solicitud.PublicId);
        }

        await EmitirAuditoriaAsync(
            usuario, AuditEventTypes.MfaRecoveryRequested, request, ahora, ct);

        return Result.Success(new RequestMfaRecoveryResult(solicitud.EjecutableDesde, enviado));
    }

    private async Task EmitirAuditoriaAsync(
        CentralUser usuario, string accion, RequestMfaRecoveryCommand request,
        DateTime ahora, CancellationToken ct)
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
                IpAddress: request.IpAddress,
                UserAgent: request.UserAgent,
                Endpoint: "/api/auth/mfa/recovery/request",
                HttpMethod: "POST",
                HttpStatusCode: 200,
                DurationMs: null,
                OccurredAt: ahora), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Accion}", accion);
        }
    }
}
