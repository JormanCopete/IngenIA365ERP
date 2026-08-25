using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Identity.KeyManagement;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// La clave de firma de los JWT se construye en UN solo sitio.
///
/// <para>
/// <b>El fallo que fija.</b> Microsoft.IdentityModel cachea los
/// <c>SignatureProvider</c> en <c>CryptoProviderFactory.Default</c>, estático de
/// proceso, y la entrada se indexa por el MATERIAL de la clave, no por el
/// objeto. Había cuatro sitios haciendo <c>new RsaSecurityKey(...)</c> con el
/// mismo PEM, así que todos caían en la misma entrada: el primero que firmaba
/// dejaba cacheado un proveedor atado a SU objeto RSA, y cuando ese objeto moría
/// —un host de prueba que se dispone, o una petición que termina— los demás
/// firmaban con una clave muerta. <c>ObjectDisposedException</c> al firmar, 500
/// en el login, sin patrón visible.
/// </para>
///
/// <para>
/// Estas pruebas existen porque el arreglo, por sí solo, era una convención:
/// nada impedía volver a escribir la línea que lo causó.
/// </para>
/// </summary>
public class UnaSolaClaveDeFirma
{
    private static readonly Regex Construccion = new(
        @"new\s+RsaSecurityKey\s*\(", RegexOptions.Compiled);

    [Fact]
    public void Nadie_mas_construye_una_clave_de_firma()
    {
        var raiz = RepoPath.FindRepoRoot();
        var culpables = new List<string>();

        foreach (var archivo in RepoPath.ProductionCSharpFiles())
        {
            if (Path.GetFileName(archivo).Equals("RsaKeyProvider.cs", StringComparison.OrdinalIgnoreCase))
                continue;

            if (Construccion.IsMatch(File.ReadAllText(archivo)))
                culpables.Add(archivo.Replace(raiz, string.Empty));
        }

        Assert.True(culpables.Count == 0,
            "la clave sale de IRsaKeyProvider.GetSecurityKey(); construir otra con el mismo " +
            "material la mete en la misma entrada del caché estático y revive el 500 al firmar. " +
            "Culpables: " + string.Join(" | ", culpables));
    }

    [Fact]
    public void El_proveedor_entrega_siempre_la_misma_clave()
    {
        // Una por host. Si devolviera una nueva cada vez volveríamos al punto de
        // partida: varias claves, mismo material, misma entrada de caché.
        var proveedor = ProveedorDePrueba();

        Assert.Same(proveedor.GetSecurityKey(), proveedor.GetSecurityKey());
    }

    [Fact]
    public void La_clave_no_usa_la_fabrica_estatica_del_proceso()
    {
        // Lo que aísla a este host de cualquier otro que cargue el mismo PEM.
        var clave = ProveedorDePrueba().GetSecurityKey();

        Assert.NotSame(CryptoProviderFactory.Default, clave.CryptoProviderFactory);
    }

    [Fact]
    public void La_clave_no_cachea_proveedores_de_firma()
    {
        // Tener fábrica propia NO cubre recargar el mismo material dentro del
        // mismo host: la entrada cacheada seguiría atada al objeto viejo. Hoy no
        // hay rotación implementada; esto la deja desarmada de antemano.
        var clave = ProveedorDePrueba().GetSecurityKey();

        Assert.False(clave.CryptoProviderFactory.CacheSignatureProviders);
    }

    [Fact]
    public void El_proveedor_no_es_desechable()
    {
        // Disponerla no libera nada útil —vive lo que vive el proceso— y sí abre
        // la ventana del apagado ordenado: con peticiones en vuelo, liberar la
        // clave convierte una firma en 500 y una validación en un 401 que en el
        // log parece un token falsificado.
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(RsaKeyProvider)));
    }

    /// <summary>Sin PEM: la guardia permite clave efímera fuera de Production.</summary>
    private static IRsaKeyProvider ProveedorDePrueba()
    {
        var previo = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            return new RsaKeyProvider(Options.Create(
                new IngenIA365ERP.Identity.Configuration.JwtSettings { PrivateKeyPath = null }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previo);
        }
    }
}
