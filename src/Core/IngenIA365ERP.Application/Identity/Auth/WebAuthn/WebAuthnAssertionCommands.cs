using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Auth.Common;
using IngenIA365ERP.Application.Identity.Auth.Login;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Auth.WebAuthn;

/// <summary>
/// Primer viaje del ingreso con passkey: emite el reto y la lista de llaves que
/// esta persona puede usar.
/// </summary>
public sealed record BeginWebAuthnAssertionCommand : IRequest<Result<BeginWebAuthnAssertionResult>>;

public sealed record BeginWebAuthnAssertionResult(string OpcionesJson, string RetoId);

public sealed class BeginWebAuthnAssertionCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IMfaDirectory credenciales,
    IWebAuthnService webAuthn,
    IWebAuthnChallengeStore retos,
    ILoginAttemptCounter contadorDeIntentos)
    : IRequestHandler<BeginWebAuthnAssertionCommand, Result<BeginWebAuthnAssertionResult>>
{
    private static readonly TimeSpan VigenciaDelReto = TimeSpan.FromMinutes(5);

    public async Task<Result<BeginWebAuthnAssertionResult>> Handle(
        BeginWebAuthnAssertionCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<BeginWebAuthnAssertionResult>(
                "Identity.Unauthenticated", "Falta el token de challenge.");
        }

        if (!string.Equals(currentUser.Purpose, CentralJwtPurposes.MfaVerify, StringComparison.Ordinal))
        {
            return Result.Failure<BeginWebAuthnAssertionResult>(
                "Identity.WrongTokenPurpose",
                $"Este endpoint requiere purpose=mfa-verify, recibido '{currentUser.Purpose}'.");
        }

        var centralUserId = currentUser.CentralUserId.Value;

        var usuario = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (usuario is null || !usuario.TwoFactorEnabled)
        {
            return Result.Failure<BeginWebAuthnAssertionResult>(
                "Identity.MfaNotEnabled", "El usuario no tiene segundo factor; reiniciá el ingreso.");
        }

        // Se CONSULTA el bloqueo pero NO se incrementa. Emitir un reto no es un
        // intento: si contara, pedir retos en bucle sería una forma gratuita de
        // bloquear a alguien, y además el escalado castigaría a quien
        // simplemente recarga la página.
        var correo = usuario.Email ?? string.Empty;
        var bloqueo = await contadorDeIntentos.CheckAsync(AmbitoDeIntentos.Mfa, correo, ct);
        if (bloqueo.IsLocked)
        {
            return Result.Failure<BeginWebAuthnAssertionResult>(
                "Identity.Locked.Soft",
                $"Demasiados intentos con el segundo factor. Reintentá en {bloqueo.RetryAfterSeconds} segundos.");
        }

        var permitidas = await credenciales.ListarWebAuthnActivasAsync(centralUserId, ct);
        if (permitidas.Count == 0)
        {
            return Result.Failure<BeginWebAuthnAssertionResult>(
                "Identity.SinPasskeys", "Esta cuenta no tiene ninguna llave inscrita.");
        }

        var opciones = webAuthn.CrearOpcionesDeIngreso(permitidas);

        var retoId = await retos.GuardarAsync(
            centralUserId, PropositoDeReto.Ingreso, opciones.ParaGuardarJson, VigenciaDelReto, ct);

        return Result.Success(new BeginWebAuthnAssertionResult(opciones.OpcionesJson, retoId));
    }
}

/// <summary>
/// Segundo viaje: llega la firma del autenticador y, si verifica, se emite la
/// sesión.
/// </summary>
public sealed record VerifyWebAuthnAssertionCommand(
    string RetoId,
    string RespuestaJson,
    string? IpAddress = null,
    string? UserAgent = null)
    : IRequest<Result<LoginResult>>;

public sealed class VerifyWebAuthnAssertionCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IMfaDirectory credenciales,
    IWebAuthnService webAuthn,
    IWebAuthnChallengeStore retos,
    IEmisorDeSesionTrasSegundoFactor emisorDeSesion,
    IAuditAppendOnlyWriter auditWriter,
    ILoginAttemptCounter contadorDeIntentos,
    IDateTimeService clock,
    ILogger<VerifyWebAuthnAssertionCommandHandler> logger)
    : IRequestHandler<VerifyWebAuthnAssertionCommand, Result<LoginResult>>
{
    public async Task<Result<LoginResult>> Handle(
        VerifyWebAuthnAssertionCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<LoginResult>(
                "Identity.Unauthenticated", "Falta el token de challenge.");
        }

        if (!string.Equals(currentUser.Purpose, CentralJwtPurposes.MfaVerify, StringComparison.Ordinal))
        {
            return Result.Failure<LoginResult>(
                "Identity.WrongTokenPurpose",
                $"Este endpoint requiere purpose=mfa-verify, recibido '{currentUser.Purpose}'.");
        }

        var centralUserId = currentUser.CentralUserId.Value;
        var ahora = clock.UtcNow;

        var usuario = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (usuario is null || !usuario.TwoFactorEnabled)
        {
            return Result.Failure<LoginResult>(
                "Identity.MfaNotEnabled", "El usuario no tiene segundo factor; reiniciá el ingreso.");
        }

        var correo = usuario.Email ?? string.Empty;
        var bloqueo = await contadorDeIntentos.CheckAsync(AmbitoDeIntentos.Mfa, correo, ct);
        if (bloqueo.IsLocked)
        {
            return Result.Failure<LoginResult>(
                "Identity.Locked.Soft",
                $"Demasiados intentos con el segundo factor. Reintentá en {bloqueo.RetryAfterSeconds} segundos.");
        }

        var opcionesOriginales = await retos.ConsumirAsync(
            centralUserId, PropositoDeReto.Ingreso, request.RetoId, ct);

        if (opcionesOriginales is null)
        {
            // Un reto vencido o ya usado NO cuenta como intento fallido: el
            // servidor no llegó a juzgar ninguna llave. Contarlo castigaría a
            // quien dejó la pantalla abierta cinco minutos.
            return Result.Failure<LoginResult>(
                "Identity.RetoVencido",
                "El desafío caducó o ya se usó. Volvé a intentar el ingreso.");
        }

        // De quién es la llave se deduce del identificador que devuelve el
        // navegador, que es único globalmente.
        var credentialId = LeerCredentialId(request.RespuestaJson);
        if (credentialId is null)
        {
            return await RegistrarFalloAsync(
                usuario, correo, request, ahora,
                "Identity.WebAuthnInvalido", "La respuesta del navegador no se pudo interpretar.", ct);
        }

        var guardada = await credenciales.BuscarWebAuthnPorCredentialIdAsync(credentialId, ct);

        // Que exista Y sea suya. Sin la segunda mitad, una llave ajena válida
        // abriría la sesión de esta persona: la firma verificaría, porque es una
        // firma legítima — de otra cuenta.
        if (guardada is null || guardada.CentralUserId != centralUserId)
        {
            return await RegistrarFalloAsync(
                usuario, correo, request, ahora,
                "Identity.WebAuthnInvalido", "La llave no se pudo verificar.", ct);
        }

        var verificacion = await webAuthn.VerificarIngresoAsync(
            request.RespuestaJson, opcionesOriginales,
            guardada.ClavePublicaCose, guardada.SignCount, ct);

        if (!verificacion.Exito)
        {
            return await RegistrarFalloAsync(
                usuario, correo, request, ahora,
                "Identity.WebAuthnInvalido",
                verificacion.Error ?? "La llave no se pudo verificar.", ct);
        }

        await credenciales.ActualizarContadorWebAuthnAsync(
            guardada.PublicId, verificacion.ContadorNuevo, verificacion.EstaRespaldada, ct);
        await credenciales.MarcarUsoAsync(centralUserId, guardada.PublicId, ahora, ct);

        await EmitirAuditoriaAsync(
            usuario.Id, usuario.Email, AuditEventTypes.CentralUserMfaSuccess, request, ahora, 200, ct);
        await contadorDeIntentos.ResetAsync(AmbitoDeIntentos.Mfa, correo, ct);

        // A partir de aquí es exactamente lo mismo que tras un TOTP correcto:
        // auto-seleccionar cooperativa, pedir que se elija, o la salida del
        // maestro global.
        return await emisorDeSesion.EmitirAsync(usuario, codigosDeRecuperacionRestantes: null, ct);
    }

    /// <summary>
    /// Un fallo que SÍ cuenta: la respuesta llegó al servidor y no verificó.
    ///
    /// <para>
    /// Lo que no cuenta —y por eso no pasa por aquí— es que la persona cancele el
    /// diálogo, se le agote el tiempo o no tenga la llave conectada: eso ni
    /// siquiera llega a la API. Si contara, cinco tropiezos legítimos dejarían a
    /// alguien sin passkey, sin TOTP y sin códigos de respaldo.
    /// </para>
    /// </summary>
    private async Task<Result<LoginResult>> RegistrarFalloAsync(
        Domain.Entities.Admin.CentralUser usuario,
        string correo,
        VerifyWebAuthnAssertionCommand request,
        DateTime ahora,
        string codigo,
        string mensaje,
        CancellationToken ct)
    {
        await EmitirAuditoriaAsync(
            usuario.Id, usuario.Email, AuditEventTypes.CentralUserMfaFailed, request, ahora, 401, ct);
        await contadorDeIntentos.RecordFailureAsync(AmbitoDeIntentos.Mfa, correo, ct);

        return Result.Failure<LoginResult>(codigo, mensaje);
    }

    /// <summary>
    /// Saca el <c>rawId</c> de la respuesta sin traerse la librería de WebAuthn a
    /// esta capa: es un campo de texto en base64url dentro de un JSON.
    /// </summary>
    private static byte[]? LeerCredentialId(string respuestaJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(respuestaJson);
            if (!doc.RootElement.TryGetProperty("rawId", out var raw)) return null;

            var texto = raw.GetString();
            if (string.IsNullOrWhiteSpace(texto)) return null;

            return Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(texto);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException)
        {
            return null;
        }
    }

    private async Task EmitirAuditoriaAsync(
        Guid centralUserId, string email, string accion,
        VerifyWebAuthnAssertionCommand request, DateTime ahora, int estado, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: centralUserId.ToString("N"),
                UserName: email,
                Action: accion,
                EntityType: nameof(Domain.Entities.Admin.CentralUser),
                EntityPublicId: centralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: request.IpAddress,
                UserAgent: request.UserAgent,
                Endpoint: "/api/auth/mfa/webauthn/verify",
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
