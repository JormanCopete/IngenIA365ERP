namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Todo lo que WebAuthn necesita del lado del servidor: emitir retos y verificar
/// las respuestas firmadas del autenticador.
///
/// <para>
/// <b>Lo que cruza esta frontera es JSON crudo.</b> Es feo a la vista, y es
/// deliberado: la librería de WebAuthn vive sólo en Infrastructure, y una prueba
/// de arquitectura impide que Application o Domain la nombren. Devolver sus tipos
/// aquí ataría toda la aplicación a una dependencia de criptografía que algún día
/// habrá que cambiar; así, sustituirla es tocar un archivo.
/// </para>
///
/// <para>
/// <b>Aquí no se verifica ninguna firma a mano.</b> Comprobar CBOR, COSE, ES256,
/// los flags del authenticator data y el hash del origen es criptografía de
/// verificación, y escribirla a mano dentro de un ERP vigilado por la
/// Superintendencia no es una opción razonable.
/// </para>
/// </summary>
public interface IWebAuthnService
{
    /// <summary>
    /// Opciones para inscribir una llave nueva, en el JSON que espera
    /// <c>navigator.credentials.create()</c>.
    /// </summary>
    /// <param name="credencialesQueYaTiene">
    /// Identificadores de sus llaves actuales. Van en <c>excludeCredentials</c>
    /// para que el navegador avise en el momento si intenta inscribir una que ya
    /// tiene, en vez de dejarla crear un duplicado que el servidor rechazará
    /// después con un mensaje mucho peor.
    /// </param>
    OpcionesDeAltaWebAuthn CrearOpcionesDeAlta(
        Guid centralUserId,
        string correo,
        string nombreVisible,
        IReadOnlyList<byte[]> credencialesQueYaTiene);

    /// <summary>
    /// Opciones para demostrar una llave, en el JSON que espera
    /// <c>navigator.credentials.get()</c>.
    /// </summary>
    OpcionesDeIngresoWebAuthn CrearOpcionesDeIngreso(
        IReadOnlyList<CredencialWebAuthnPermitida> credencialesPermitidas);

    /// <summary>
    /// Verifica la respuesta de un alta contra el reto que se emitió.
    /// </summary>
    /// <param name="respuestaJson">Lo que devolvió el navegador, tal cual.</param>
    /// <param name="opcionesOriginalesJson">Las opciones que se guardaron al emitir el reto.</param>
    Task<ResultadoDeAltaWebAuthn> VerificarAltaAsync(
        string respuestaJson,
        string opcionesOriginalesJson,
        Func<byte[], CancellationToken, Task<bool>> elCredentialIdEstaLibre,
        CancellationToken ct);

    /// <summary>
    /// Verifica la respuesta de un ingreso contra el reto y la clave pública
    /// guardada.
    /// </summary>
    Task<ResultadoDeIngresoWebAuthn> VerificarIngresoAsync(
        string respuestaJson,
        string opcionesOriginalesJson,
        byte[] clavePublicaCose,
        long contadorGuardado,
        CancellationToken ct);
}

/// <param name="OpcionesJson">Lo que se le entrega al navegador.</param>
/// <param name="ParaGuardarJson">
/// Lo mismo, pero es lo que hay que conservar para verificar la respuesta. Se
/// devuelve por separado para que quede claro que el servidor tiene que
/// recordarlo: verificar contra unas opciones distintas de las presentadas es
/// exactamente lo que el reto existe para impedir.
/// </param>
public sealed record OpcionesDeAltaWebAuthn(string OpcionesJson, string ParaGuardarJson);

public sealed record OpcionesDeIngresoWebAuthn(string OpcionesJson, string ParaGuardarJson);

/// <summary>Una llave que se le permite usar a quien está entrando.</summary>
public sealed record CredencialWebAuthnPermitida(byte[] CredentialId, string? Transports);

/// <param name="Error">Null si salió bien. Si no, qué pasó, en lenguaje llano.</param>
public sealed record ResultadoDeAltaWebAuthn(
    bool Exito,
    string? Error,
    byte[]? CredentialId,
    byte[]? ClavePublicaCose,
    long ContadorDeFirmas,
    Guid? AaGuid,
    string? TransportsJson,
    bool EsRespaldable,
    bool EstaRespaldada,
    string? FormatoDeAtestacion);

/// <param name="ContadorNuevo">
/// El que reportó el autenticador. Puede ser igual al guardado: la mayoría de las
/// passkeys de plataforma reportan siempre cero, y eso NO es sospechoso.
/// </param>
public sealed record ResultadoDeIngresoWebAuthn(
    bool Exito,
    string? Error,
    long ContadorNuevo,
    bool EstaRespaldada);
