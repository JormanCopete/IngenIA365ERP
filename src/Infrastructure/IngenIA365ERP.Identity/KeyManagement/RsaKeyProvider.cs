using System.Security.Cryptography;
using IngenIA365ERP.Identity.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IngenIA365ERP.Identity.KeyManagement;

/// <summary>
/// La clave RSA con la que se firman y se validan los JWT RS256. Se carga del
/// PEM en <see cref="JwtSettings.PrivateKeyPath"/>.
///
/// <para>
/// Si el archivo no existe: en <b>Production</b> el arranque FALLA (una clave
/// efímera emitiría tokens que mueren en cada reinicio y rompería la sesión de
/// todos los usuarios en silencio); en dev/tests se genera una en memoria para
/// no exigir secretos montados.
/// </para>
/// </summary>
public interface IRsaKeyProvider
{
    /// <summary>
    /// La clave, ya envuelta. <b>Esta es la única forma de obtenerla</b>, y eso
    /// es la mitad del arreglo.
    ///
    /// <para>
    /// <b>Qué pasó.</b> Microsoft.IdentityModel cachea los
    /// <c>SignatureProvider</c> en <c>CryptoProviderFactory.Default</c>, que es
    /// estático de proceso, y la entrada del caché se indexa por el MATERIAL de
    /// la clave, no por el objeto. Dos <c>ServiceProvider</c> que carguen el
    /// mismo PEM caen en la misma entrada: el primero que firma deja cacheado un
    /// proveedor atado a SU objeto RSA y, cuando ese objeto muere, los demás
    /// siguen recibiendo el proveedor muerto. El síntoma es un 500 al firmar
    /// —<c>ObjectDisposedException: RSABCrypt</c>— en un host cuya propia clave
    /// está viva.
    /// </para>
    ///
    /// <para>
    /// Antes había cuatro sitios construyendo <c>new RsaSecurityKey(...)</c> por
    /// su cuenta, y el peor no era el de las pruebas: <c>JwtService</c> es
    /// <i>scoped</i> y creaba un RSA por petición con el mismo material, así que
    /// el proveedor cacheado quedaba atado al RSA de la primera petición, que
    /// después nadie mantenía vivo. Eso es un 500 intermitente en el login, en
    /// producción, sin patrón visible.
    /// </para>
    ///
    /// <para>
    /// Por eso no hay un <c>GetKey()</c> que devuelva el <see cref="RSA"/> pelado:
    /// existía, se quedó sin un solo consumidor, y dejarlo mantenía escribible
    /// —y natural— la línea exacta que causó la caída. La invariante la sostiene
    /// el tipo, no un comentario.
    /// </para>
    /// </summary>
    RsaSecurityKey GetSecurityKey();
}

/// <summary>
/// Una clave por host, con fábrica criptográfica propia y sin caché de
/// proveedores.
///
/// <para>
/// <b>Deliberadamente NO implementa <see cref="IDisposable"/>.</b> Vive lo que
/// vive el proceso, así que disponerla no libera nada útil y sí abre la ventana
/// de fallo: durante un apagado ordenado, con peticiones aún en vuelo, liberar
/// la clave convierte una firma en un 500 y una validación en un 401 que en el
/// log parece un token falsificado. Antes se disponía, y ese era el gatillo.
/// </para>
/// </summary>
public class RsaKeyProvider : IRsaKeyProvider
{
    private readonly RsaSecurityKey _securityKey;

    public RsaKeyProvider(IOptions<JwtSettings> settings)
    {
        var rsa = RSA.Create();
        var path = settings.Value.PrivateKeyPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            rsa.ImportFromPem(File.ReadAllText(path));
        }
        else
        {
            RsaKeyGuard.ThrowIfProduction(path);
            rsa.KeySize = 2048;
        }

        _securityKey = new RsaSecurityKey(rsa)
        {
            // Fábrica propia Y sin caché. Lo primero aísla este host de los
            // demás; lo segundo cubre el caso que la fábrica propia NO cubre:
            // dentro de un mismo host, recargar el mismo material en un objeto
            // RSA nuevo reutiliza la entrada cacheada del anterior y vuelve a
            // firmar con una clave muerta. Hoy no hay rotación implementada;
            // el día que la haya, la trampa ya está desarmada.
            //
            // Medido con el PEM real, 1000 firmas: 0,721 ms/token como estaba
            // (RsaSecurityKey nuevo por token sobre la fábrica Default), 0,518
            // con caché propio, 0,574 así. Apagar el caché cuesta 0,056 ms por
            // token y sigue siendo más barato que lo que había.
            CryptoProviderFactory = new CryptoProviderFactory
            {
                CacheSignatureProviders = false,
            },
        };
    }

    public RsaSecurityKey GetSecurityKey() => _securityKey;
}

/// <summary>
/// Guardia compartida: prohíbe el fallback a clave RSA efímera cuando el
/// entorno es Production (o no está definido — el default de ASP.NET Core
/// ES Production). Hardening surgido del integration test T118, donde el
/// fallback silencioso produjo emisor y validador con claves distintas.
/// </summary>
internal static class RsaKeyGuard
{
    public static void ThrowIfProduction(string? configuredPath)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                  ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var isProduction = string.IsNullOrWhiteSpace(env)
                           || env.Equals("Production", StringComparison.OrdinalIgnoreCase);
        if (isProduction)
        {
            throw new InvalidOperationException(
                $"Clave RSA de firma JWT no encontrada en '{configuredPath}'. " +
                "En Production la clave PEM debe estar montada (JwtSettings:PrivateKeyPath); " +
                "sin ella se generaría una clave efímera y todos los tokens emitidos " +
                "morirían en cada reinicio del servicio.");
        }
    }
}
