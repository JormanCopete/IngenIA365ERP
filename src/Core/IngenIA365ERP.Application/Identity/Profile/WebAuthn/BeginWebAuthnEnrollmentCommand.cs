using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.WebAuthn;

/// <summary>
/// Primer viaje del alta de una passkey: emite el reto y las opciones que el
/// navegador necesita para pedirle la llave a la persona.
/// </summary>
public sealed record BeginWebAuthnEnrollmentCommand : IRequest<Result<BeginWebAuthnEnrollmentResult>>;

/// <param name="OpcionesJson">Se le pasa tal cual a <c>navigator.credentials.create()</c>.</param>
/// <param name="RetoId">
/// Con qué se recupera el reto en el segundo viaje. El reto en sí no viaja de
/// vuelta: se queda en el servidor, que es lo que impide reproducir una respuesta
/// capturada.
/// </param>
public sealed record BeginWebAuthnEnrollmentResult(string OpcionesJson, string RetoId);

public sealed class BeginWebAuthnEnrollmentCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IMfaDirectory credenciales,
    IWebAuthnService webAuthn,
    IWebAuthnChallengeStore retos)
    : IRequestHandler<BeginWebAuthnEnrollmentCommand, Result<BeginWebAuthnEnrollmentResult>>
{
    /// <summary>
    /// Igual que el del TOTP: el ingreso prueba las credenciales de la persona, y
    /// sin tope eso deja de estar acotado.
    /// </summary>
    private const int MaximoDeAutenticadores = 5;

    /// <summary>
    /// Lo que tarda alguien en sacar la llave del bolsillo o mirar el teléfono.
    /// Corto a propósito: un reto vivo mucho tiempo es una ventana abierta.
    /// </summary>
    private static readonly TimeSpan VigenciaDelReto = TimeSpan.FromMinutes(5);

    public async Task<Result<BeginWebAuthnEnrollmentResult>> Handle(
        BeginWebAuthnEnrollmentCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<BeginWebAuthnEnrollmentResult>(
                "Identity.Unauthenticated", "Se requiere autenticación válida.");
        }

        // Igual que el alta de TOTP: vale la sesión normal o el token de
        // inscripción forzada, para que quien entra por primera vez con la
        // política activa pueda inscribir una passkey sin pasar antes por TOTP.
        if (currentUser.Purpose != CentralJwtPurposes.Full
            && currentUser.Purpose != CentralJwtPurposes.MfaEnroll)
        {
            return Result.Failure<BeginWebAuthnEnrollmentResult>(
                "Identity.WrongTokenPurpose",
                "Este endpoint requiere purpose=full o mfa-enroll.");
        }

        var centralUserId = currentUser.CentralUserId.Value;

        var usuario = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (usuario is null)
        {
            return Result.Failure<BeginWebAuthnEnrollmentResult>(
                "Identity.Unauthenticated", "Usuario no encontrado.");
        }

        // Por tipo, no sobre el total: cinco apps de códigos no pueden impedir
        // inscribir la primera llave. Ver el mismo comentario en el alta de TOTP.
        var yaInscritas = await credenciales.ContarActivasDeTipoAsync(
            centralUserId, Domain.Entities.Admin.MetodosMfa.WebAuthn, ct);

        if (yaInscritas >= MaximoDeAutenticadores)
        {
            return Result.Failure<BeginWebAuthnEnrollmentResult>(
                "Profile.Mfa.DemasiadasCredenciales",
                $"Ya tenés {yaInscritas} llaves inscritas, que es el máximo. " +
                "Retirá alguna que ya no uses antes de agregar otra.");
        }

        // Las llaves que ya tiene van en excludeCredentials: así el navegador
        // avisa en el momento si intenta inscribir una repetida, en vez de dejarla
        // crear un duplicado que el servidor rechazaría después con un mensaje
        // mucho peor.
        var yaTiene = await credenciales.ListarWebAuthnActivasAsync(centralUserId, ct);

        var opciones = webAuthn.CrearOpcionesDeAlta(
            centralUserId,
            usuario.Email,
            usuario.Email,
            [.. yaTiene.Select(c => c.CredentialId)]);

        var retoId = await retos.GuardarAsync(
            centralUserId, PropositoDeReto.Alta, opciones.ParaGuardarJson, VigenciaDelReto, ct);

        return Result.Success(new BeginWebAuthnEnrollmentResult(opciones.OpcionesJson, retoId));
    }
}
