using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Identity.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Identity.WebAuthn;

/// <summary>
/// Envoltura de Fido2NetLib. Es el ÚNICO archivo del proyecto que conoce esa
/// librería: hacia afuera sólo salen tipos propios y JSON crudo, y una prueba de
/// arquitectura impide que Application o Domain la nombren.
///
/// <para>
/// La criptografía la hace la librería y no este archivo, a propósito. Verificar
/// CBOR, COSE, ES256, los flags del authenticator data y el hash del origen es
/// exactamente el tipo de código que no se escribe a mano dentro de un ERP
/// financiero.
/// </para>
/// </summary>
internal sealed class WebAuthnService : IWebAuthnService
{
    private readonly IFido2 _fido2;
    private readonly WebAuthnOptions _opciones;
    private readonly ILogger<WebAuthnService> _log;

    public WebAuthnService(
        IOptions<WebAuthnOptions> opciones,
        ILogger<WebAuthnService> log)
    {
        _opciones = opciones.Value;
        _log = log;

        _fido2 = new Fido2(new Fido2Configuration
        {
            ServerDomain = _opciones.RelyingPartyId,
            ServerName = _opciones.RelyingPartyName,
            Origins = new HashSet<string>(_opciones.OrigenesPermitidos, StringComparer.OrdinalIgnoreCase),
            TimestampDriftTolerance = _opciones.TimeoutMs,
        });
    }

    public OpcionesDeAltaWebAuthn CrearOpcionesDeAlta(
        Guid centralUserId,
        string correo,
        string nombreVisible,
        IReadOnlyList<byte[]> credencialesQueYaTiene)
    {
        var usuario = new Fido2User
        {
            // El identificador de usuario que viaja al autenticador es el Guid
            // interno, NO el correo: queda guardado dentro del dispositivo, y un
            // cambio de correo no puede invalidar una llave.
            Id = centralUserId.ToByteArray(),
            Name = correo,
            DisplayName = string.IsNullOrWhiteSpace(nombreVisible) ? correo : nombreVisible,
        };

        var opciones = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = usuario,

            // Para que el navegador avise EN EL MOMENTO si intenta inscribir una
            // llave que ya tiene, en vez de dejarla crear un duplicado que el
            // servidor rechazaría después con un mensaje mucho peor.
            ExcludeCredentials = [.. credencialesQueYaTiene.Select(id => new PublicKeyCredentialDescriptor(id))],

            AuthenticatorSelection = AuthenticatorSelection.Default,

            // Sin atestación: no se pide al fabricante que certifique el modelo.
            // Pedirla obligaría a mantener una lista de metadatos y a decidir qué
            // marcas se aceptan, y no aporta nada mientras no haya una política
            // que diga «sólo llaves certificadas».
            AttestationPreference = AttestationConveyancePreference.None,
        });

        var json = opciones.ToJson();
        return new OpcionesDeAltaWebAuthn(json, json);
    }

    public OpcionesDeIngresoWebAuthn CrearOpcionesDeIngreso(
        IReadOnlyList<CredencialWebAuthnPermitida> credencialesPermitidas)
    {
        var opciones = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [.. credencialesPermitidas.Select(
                c => new PublicKeyCredentialDescriptor(c.CredentialId))],
            UserVerification = UserVerificationRequirement.Preferred,
        });

        var json = opciones.ToJson();
        return new OpcionesDeIngresoWebAuthn(json, json);
    }

    public async Task<ResultadoDeAltaWebAuthn> VerificarAltaAsync(
        string respuestaJson,
        string opcionesOriginalesJson,
        Func<byte[], CancellationToken, Task<bool>> elCredentialIdEstaLibre,
        CancellationToken ct)
    {
        try
        {
            var respuesta = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(respuestaJson);
            if (respuesta is null) return Fallo("La respuesta del navegador no se pudo interpretar.");

            var opciones = CredentialCreateOptions.FromJson(opcionesOriginalesJson);

            var resultado = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = respuesta,
                OriginalOptions = opciones,
                IsCredentialIdUniqueToUserCallback = async (args, token) =>
                    await elCredentialIdEstaLibre(args.CredentialId, token),
            }, ct);

            return new ResultadoDeAltaWebAuthn(
                Exito: true,
                Error: null,
                CredentialId: resultado.Id,
                ClavePublicaCose: resultado.PublicKey,
                ContadorDeFirmas: resultado.SignCount,
                AaGuid: resultado.AaGuid,
                TransportsJson: resultado.Transports is { Length: > 0 } t
                    ? JsonSerializer.Serialize(t.Select(x => x.ToString()))
                    : null,
                EsRespaldable: resultado.IsBackupEligible,
                EstaRespaldada: resultado.IsBackedUp,
                FormatoDeAtestacion: resultado.AttestationFormat);
        }
        catch (Fido2VerificationException ex)
        {
            // El mensaje de la librería es técnico y va al registro; a la persona
            // se le dice algo que pueda usar.
            _log.LogWarning(ex, "El alta de la passkey no verificó.");
            return Fallo("No se pudo verificar la llave. Volvé a intentarlo.");
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "La respuesta de alta de passkey no es JSON válido.");
            return Fallo("La respuesta del navegador no se pudo interpretar.");
        }
    }

    public async Task<ResultadoDeIngresoWebAuthn> VerificarIngresoAsync(
        string respuestaJson,
        string opcionesOriginalesJson,
        byte[] clavePublicaCose,
        long contadorGuardado,
        CancellationToken ct)
    {
        try
        {
            var respuesta = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(respuestaJson);
            if (respuesta is null)
                return new ResultadoDeIngresoWebAuthn(false, "La respuesta del navegador no se pudo interpretar.", 0, false);

            var opciones = AssertionOptions.FromJson(opcionesOriginalesJson);

            var resultado = await _fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = respuesta,
                OriginalOptions = opciones,
                StoredPublicKey = clavePublicaCose,

                // El contador guardado. La librería rechaza si el que llega es
                // MENOR, que es la señal de que la llave se clonó.
                StoredSignatureCounter = (uint)contadorGuardado,

                // Quién es el dueño ya lo resolvió el llamador por el credential
                // id, que es único globalmente. Aquí se acepta siempre porque la
                // pregunta ya está contestada aguas arriba.
                IsUserHandleOwnerOfCredentialIdCallback = (args, token) => Task.FromResult(true),
            }, ct);

            return new ResultadoDeIngresoWebAuthn(
                Exito: true,
                Error: null,
                ContadorNuevo: resultado.SignCount,
                EstaRespaldada: resultado.IsBackedUp);
        }
        catch (Fido2VerificationException ex)
        {
            _log.LogWarning(ex, "La aserción de la passkey no verificó.");
            return new ResultadoDeIngresoWebAuthn(false, "La llave no se pudo verificar.", 0, false);
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "La respuesta de ingreso con passkey no es JSON válido.");
            return new ResultadoDeIngresoWebAuthn(
                false, "La respuesta del navegador no se pudo interpretar.", 0, false);
        }
    }

    private static ResultadoDeAltaWebAuthn Fallo(string mensaje) =>
        new(false, mensaje, null, null, 0, null, null, false, false, null);
}
