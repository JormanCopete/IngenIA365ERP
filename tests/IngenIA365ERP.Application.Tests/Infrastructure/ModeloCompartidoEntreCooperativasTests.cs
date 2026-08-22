using System.Diagnostics;
using FluentAssertions;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Mediciones que deciden si "una base de datos por cooperativa" es viable.
///
/// <para>
/// EF Core construye el modelo —272 entidades— una vez por <i>clave de caché</i>.
/// Si esa clave incluyera la cooperativa, cada una pagaría su propia construcción
/// y el coste sería prohibitivo con decenas de cooperativas. Con aislamiento por
/// base todas comparten esquema, así que el modelo debería ser uno solo y
/// compartido. Estas pruebas lo comprueban en vez de darlo por hecho.
/// </para>
///
/// <para>
/// No abren ninguna conexión: EF construye el modelo de forma perezosa y sin
/// hablar con el servidor, así que las cadenas apuntan a bases que no existen.
/// </para>
/// </summary>
public class ModeloCompartidoEntreCooperativasTests
{
    private const string Base = "Host=localhost;Port=5432;Username=x;Password=y;Database=";

    private static DbContextOptions<ApplicationDbContext> Opciones(string baseDeDatos) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(Base + baseDeDatos)
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    [Fact]
    public void DosCooperativasEnBasesDistintas_CompartenElMismoModelo()
    {
        // La medición que decide el enfoque. Si esto falla, cada cooperativa
        // construye sus 272 entidades por separado y hay que replantear.
        using var alfa = new ApplicationDbContext(Opciones("erp_coop_alfa"));
        using var beta = new ApplicationDbContext(Opciones("erp_coop_beta"));

        ReferenceEquals(alfa.Model, beta.Model).Should().BeTrue(
            "con una base por cooperativa todas comparten esquema, así que el modelo de EF " +
            "debe construirse una sola vez por proceso");
    }

    [Fact]
    public void ElSegundoModelo_NoCuestaConstruirlo()
    {
        // Corolario medible del anterior: si el modelo se reutiliza, abrir el
        // contexto de la segunda cooperativa es inmediato.
        using var primera = new ApplicationDbContext(Opciones("erp_coop_uno"));
        _ = primera.Model;   // paga la construcción

        var reloj = Stopwatch.StartNew();
        using var segunda = new ApplicationDbContext(Opciones("erp_coop_dos"));
        _ = segunda.Model;
        reloj.Stop();

        reloj.ElapsedMilliseconds.Should().BeLessThan(250,
            "reutilizar el modelo tiene que ser barato; si tarda, es que lo reconstruyó");
    }

    [Fact]
    public void ElEsquemaSigueSiendoDboEnTodasLasCooperativas()
    {
        // Con base por cooperativa el esquema deja de discriminar: cada base tiene
        // su propio dbo. Es lo que permite compartir el modelo.
        using var db = new ApplicationDbContext(
            Opciones("erp_coop_alfa"), new ErpTenantInfo { SchemaName = "dbo" });

        db.Model.GetDefaultSchema().Should().Be("dbo");
    }

    [Fact]
    public void LaLambdaDeAddDbContext_SeEvaluaUnaVezPorAmbito()
    {
        // Decide si la cadena de conexión se puede resolver por petición desde el
        // registro de siempre, o si hace falta construir las opciones a mano.
        var veces = 0;
        var servicios = new ServiceCollection();
        servicios.AddDbContext<ApplicationDbContext>((_, opciones) =>
        {
            veces++;
            opciones.UseNpgsql(Base + "erp_medicion")
                    .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        });

        using var raiz = servicios.BuildServiceProvider();
        using (var a = raiz.CreateScope()) { _ = a.ServiceProvider.GetRequiredService<ApplicationDbContext>(); }
        using (var b = raiz.CreateScope()) { _ = b.ServiceProvider.GetRequiredService<ApplicationDbContext>(); }

        veces.Should().Be(2,
            "si se evaluara una sola vez, la cadena quedaria congelada en la primera " +
            "cooperativa que entrase y todas las demas leerian sus datos");
    }
}
