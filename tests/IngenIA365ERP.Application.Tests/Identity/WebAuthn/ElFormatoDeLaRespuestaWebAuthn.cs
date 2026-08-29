using System.Text.Json;
using Fido2NetLib;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.WebAuthn;

/// <summary>
/// El contrato entre <c>webauthn.js</c> y la librería del servidor.
///
/// <para>
/// <b>Por qué existe esta prueba.</b> Entre el navegador y el servidor viaja un
/// JSON cuyos nombres de campo no los elige nadie de este equipo: los pone la
/// especificación de un lado y Fido2NetLib del otro. Equivocarse en uno solo no
/// da error de compilación ni excepción clara — da un campo en null que termina
/// en «no se pudo verificar la llave», que es exactamente el mismo mensaje que
/// sale cuando la firma está mal de verdad. Se depura mirando bytes.
/// </para>
///
/// <para>
/// Este repositorio no tiene pruebas de navegador, así que el módulo JS no se
/// ejecuta en la suite. Lo que sí se puede comprobar, y es donde de verdad se
/// rompen estas integraciones, son las dos mitades del contrato: que el servidor
/// entiende la forma que el módulo escribe, y que el módulo sigue escribiendo esa
/// forma. Las dos pruebas juntas se caen si alguien renombra un campo en
/// cualquiera de los dos lados.
/// </para>
///
/// <para>
/// Lo que NO prueba: nada criptográfico. Los valores de abajo son bytes
/// inventados, no una atestación real — <b>fabricar una exigiría un autenticador
/// de verdad</b>. Verificar la firma es trabajo de la librería y está probado río
/// arriba; lo que se afirma aquí es que los bytes llegan enteros al sitio
/// correcto.
/// </para>
/// </summary>
public class ElFormatoDeLaRespuestaWebAuthn
{
    private static readonly byte[] CredentialId = [0x01, 0x02, 0x03, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF];
    private static readonly byte[] ObjetoDeAtestacion = [0xA3, 0x63, 0x66, 0x6D, 0x74, 0x00, 0x7E];
    private static readonly byte[] DatosDelCliente = [0x7B, 0x22, 0x74, 0x79, 0x70];
    private static readonly byte[] DatosDelAutenticador = [0x49, 0x96, 0x0D, 0xE5, 0xFF];
    private static readonly byte[] Firma = [0x30, 0x45, 0x02, 0x21, 0xF0];
    private static readonly byte[] ManejadorDeUsuario = [0xAA, 0xBB, 0xCC, 0xDD];

    /// <summary>
    /// Los bytes se eligieron con valores por encima de 0x7F a propósito: son los
    /// que producen <c>+</c> y <c>/</c> en base64 estándar y <c>-</c> y <c>_</c> en
    /// base64url. Con bytes bajos las dos codificaciones coinciden y la prueba
    /// pasaría aunque el módulo usara la equivocada.
    /// </summary>
    private static string B64(byte[] bytes) => WebEncoders.Base64UrlEncode(bytes);

    /// <summary>
    /// Los vectores de arriba sólo sirven si distinguen las dos codificaciones. Con
    /// bytes bajos, base64 y base64url dan la misma cadena, y las pruebas pasarían
    /// aunque el módulo usara la equivocada — que es un fallo real: el navegador
    /// devuelve base64url y la librería espera base64url, pero un helper copiado de
    /// cualquier ejemplo suele devolver base64 a secas.
    /// </summary>
    [Fact]
    public void Los_vectores_distinguen_base64url_de_base64()
    {
        byte[][] vectores =
        [
            CredentialId, ObjetoDeAtestacion, DatosDelAutenticador, Firma, ManejadorDeUsuario,
        ];

        var distinguen = vectores.Count(
            v => B64(v) != Convert.ToBase64String(v).TrimEnd('='));

        Assert.True(distinguen > 0,
            "Ningún vector de prueba contiene bytes que produzcan '+' o '/' en base64. " +
            "Tal como están, las pruebas de formato no notarían una codificación equivocada.");
    }

    [Fact]
    public void El_alta_que_escribe_el_modulo_la_entiende_el_servidor()
    {
        // Copiado de la forma que arma `inscribir()` en webauthn.js.
        var json = $$"""
        {
          "id": "{{B64(CredentialId)}}",
          "rawId": "{{B64(CredentialId)}}",
          "type": "public-key",
          "response": {
            "attestationObject": "{{B64(ObjetoDeAtestacion)}}",
            "clientDataJSON": "{{B64(DatosDelCliente)}}",
            "transports": ["internal", "hybrid"]
          },
          "extensions": {},
          "clientExtensionResults": {}
        }
        """;

        var respuesta = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(json);

        Assert.NotNull(respuesta);

        // `id` viaja como texto y `rawId` como bytes: son el mismo valor con dos
        // representaciones, y la librería los expone así. Se comprueban los dos
        // porque el módulo los escribe por separado —uno lo da el navegador ya
        // codificado y el otro lo codifica el módulo— y podrían divergir.
        Assert.Equal(B64(CredentialId), respuesta!.Id);
        Assert.Equal(CredentialId, respuesta.RawId);
        Assert.Equal(ObjetoDeAtestacion, respuesta.Response.AttestationObject);
        Assert.Equal(DatosDelCliente, respuesta.Response.ClientDataJson);
    }

    [Fact]
    public void El_ingreso_que_escribe_el_modulo_lo_entiende_el_servidor()
    {
        // Copiado de la forma que arma `firmar()` en webauthn.js.
        var json = $$"""
        {
          "id": "{{B64(CredentialId)}}",
          "rawId": "{{B64(CredentialId)}}",
          "type": "public-key",
          "response": {
            "authenticatorData": "{{B64(DatosDelAutenticador)}}",
            "clientDataJSON": "{{B64(DatosDelCliente)}}",
            "signature": "{{B64(Firma)}}",
            "userHandle": "{{B64(ManejadorDeUsuario)}}"
          },
          "extensions": {},
          "clientExtensionResults": {}
        }
        """;

        var respuesta = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(json);

        Assert.NotNull(respuesta);
        Assert.Equal(CredentialId, respuesta!.RawId);
        Assert.Equal(DatosDelAutenticador, respuesta.Response.AuthenticatorData);
        Assert.Equal(DatosDelCliente, respuesta.Response.ClientDataJson);
        Assert.Equal(Firma, respuesta.Response.Signature);
        Assert.Equal(ManejadorDeUsuario, respuesta.Response.UserHandle);
    }

    /// <summary>
    /// La otra mitad. Sin esto, la prueba de arriba seguiría verde después de
    /// renombrar un campo en el JS: comprobaría que el servidor entiende una forma
    /// que ya nadie le manda.
    /// </summary>
    [Fact]
    public void El_modulo_js_sigue_escribiendo_esos_campos()
    {
        var modulo = CodigoDelModulo();

        // Lo que SALE hacia el servidor, con los mismos nombres que las pruebas de
        // arriba deserializan.
        string[] campos =
        [
            "rawId:", "attestationObject:", "clientDataJSON:",
            "authenticatorData:", "signature:", "userHandle:",
        ];

        var faltan = campos.Where(c => !modulo.Contains(c, StringComparison.Ordinal)).ToList();

        Assert.True(faltan.Count == 0,
            "webauthn.js dejó de escribir estos campos, y el servidor los espera:\n  " +
            string.Join("\n  ", faltan));
    }

    /// <summary>
    /// Lo que ENTRA, que es la mitad que se olvida. Un campo sin decodificar no da
    /// un error del servidor: da un <c>TypeError</c> del navegador —
    /// «challenge: Value is not of type BufferSource»— que sólo se ve con la
    /// consola abierta.
    /// </summary>
    [Fact]
    public void El_modulo_js_sigue_decodificando_lo_que_llega()
    {
        var modulo = CodigoDelModulo();

        string[] conversiones =
        [
            "opciones.challenge = aBytes(",
            "opciones.user.id = aBytes(",
            "opciones.excludeCredentials = convertirDescriptores(",
            "opciones.allowCredentials = convertirDescriptores(",
        ];

        var faltan = conversiones.Where(c => !modulo.Contains(c, StringComparison.Ordinal)).ToList();

        Assert.True(faltan.Count == 0,
            "webauthn.js dejó de convertir estos campos de base64url a bytes:\n  " +
            string.Join("\n  ", faltan));
    }

    /// <summary>
    /// El módulo <b>sin sus comentarios</b>.
    ///
    /// <para>
    /// Leerlo entero no servía: comentar una línea la deja intacta para
    /// <c>Contains</c>, y comentar es justamente cómo se rompe esto de verdad —
    /// alguien depura, silencia una conversión, y no la vuelve a poner. Se
    /// comprobó: con <c>// opciones.user.id = aBytes(…)</c> la prueba seguía en
    /// verde.
    /// </para>
    /// </summary>
    private static string CodigoDelModulo()
    {
        var lineas = File.ReadAllLines(RutaDelModulo())
            .Select(l => l.TrimStart())
            .Where(l => !l.StartsWith("//", StringComparison.Ordinal)
                     && !l.StartsWith("/*", StringComparison.Ordinal)
                     && !l.StartsWith("*", StringComparison.Ordinal));

        return string.Join('\n', lineas);
    }

    private static string RutaDelModulo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IngenIA365ERP.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(
            dir!.FullName, "src", "Presentation", "IngenIA365ERP.Shared",
            "wwwroot", "js", "webauthn.js");
    }
}
