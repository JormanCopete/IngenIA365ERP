using IngenIA365ERP.Persistence.DbContext;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// El llavero de DataProtection tiene que vivir fuera del proceso.
///
/// <para>
/// <c>AddDataProtection()</c> estaba registrado sin <c>PersistKeysTo*</c>. Sin esa
/// llamada las claves van al perfil del proceso, que en un contenedor Linux es su
/// capa escribible. Consecuencias, las dos vistas en producción:
/// </para>
///
/// <list type="bullet">
/// <item>Cada despliegue rota los pods y con ellos el llavero, así que todo lo
/// cifrado antes deja de descifrarse.</item>
/// <item>Con dos réplicas de API hay dos llaveros distintos: lo que cifra un pod,
/// el otro no lo abre. Intermitente, más o menos la mitad de las veces.</item>
/// </list>
///
/// <para>
/// Ese llavero no protege sólo el segundo factor: envuelve también la clave de
/// cada adjunto (<c>AttachmentEncryptionService</c>). Perderlo no es un problema
/// de login, es que los archivos cifrados dejan de poder leerse.
/// </para>
///
/// <para>
/// Las pruebas de MFA que ya existen NO cazan esto: cifran y descifran dentro del
/// mismo proceso, donde un llavero en memoria funciona perfectamente. Por eso esta
/// prueba construye un proveedor <b>independiente</b>, con su propio contenedor de
/// dependencias, apuntando a la misma base: es lo más parecido a «otro pod» que se
/// puede montar sin levantar otro pod. Si alguien quita
/// <c>PersistKeysToDbContext</c>, el segundo proveedor genera su propia clave y no
/// puede abrir lo del primero.
/// </para>
/// </summary>
public sealed class ElLlaveroSobreviveAlProceso(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    /// <summary>
    /// Tiene que coincidir con el de <c>Identity/DependencyInjection.cs</c>: es el
    /// discriminador de aislamiento de DataProtection. Si no coincide, el segundo
    /// proveedor no reconoce el llavero aunque lo esté leyendo.
    /// </summary>
    private const string NombreDeAplicacion = "IngenIA365ERP";

    [Fact]
    public void Lo_que_cifra_una_instancia_lo_descifra_otra_distinta()
    {
        const string proposito = "prueba:llavero-entre-procesos";
        const string secreto = "JBSWY3DPEHPK3PXP";

        // 1) La instancia que ya corre cifra algo.
        using var scopeApp = fx.Factory.Services.CreateScope();
        var protectorApp = scopeApp.ServiceProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(proposito);
        var cifrado = protectorApp.Protect(secreto);

        // 2) Una instancia NUEVA, con su propio contenedor, sobre la misma base.
        var cadena = scopeApp.ServiceProvider
            .GetRequiredService<AdminDbContext>()
            .Database.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(cadena));

        using var otraInstancia = ConstruirInstanciaIndependiente(cadena!);
        using var scopeOtra = otraInstancia.CreateScope();
        var protectorOtra = scopeOtra.ServiceProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(proposito);

        // 3) Si el llavero fuera del proceso, esto lanzaría CryptographicException.
        var descifrado = protectorOtra.Unprotect(cifrado);

        Assert.Equal(secreto, descifrado);
    }

    [Fact]
    public void El_llavero_quedo_persistido_en_la_base_administrativa()
    {
        // Forzar la creación de al menos una clave.
        using var scope = fx.Factory.Services.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("prueba:hay-llavero")
            .Protect("x");

        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var claves = db.DataProtectionKeys.AsNoTracking().ToList();

        Assert.NotEmpty(claves);
        Assert.All(claves, k => Assert.False(string.IsNullOrWhiteSpace(k.Xml)));
    }

    private static ServiceProvider ConstruirInstanciaIndependiente(string cadena)
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();

        servicios.AddDbContext<AdminDbContext>(opciones =>
        {
            if (CentralIdentityApiFixture.ProviderKey == "SqlServer")
            {
                opciones.UseSqlServer(cadena);
            }
            else
            {
                opciones.UseNpgsql(cadena);
            }
        });

        servicios.AddDataProtection()
            .PersistKeysToDbContext<AdminDbContext>()
            .SetApplicationName(NombreDeAplicacion);

        return servicios.BuildServiceProvider();
    }
}
