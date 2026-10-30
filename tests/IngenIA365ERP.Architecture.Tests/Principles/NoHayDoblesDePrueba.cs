using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0003 y T0297 (R-26; FR-223; SC-027; TR32): con Recaudo y Tesorería reales no hay dobles de prueba en ningún ambiente. Reemplaza a
/// <c>LosDoblesDePruebaNoSeRegistranEnProduccion</c> de la 013, que vigilaba que los dobles sólo se registraran fuera de producción, desde que la fase 19
/// (T0307, 2026-10-26) los borró:
/// <list type="bullet">
/// <item>el consumidor de los destinos <c>Receipts</c> y <c>Treasury</c> es el del módulo (<c>DestinoRecaudo</c>, <c>DestinoTesoreria</c>): ningún otro tipo
/// de la aplicación implementa <see cref="IDestinoDeMensajes"/> en esos espacios, y ninguno en ningún espacio se llama como un doble;</item>
/// <item>no existe el espacio <c>Lending.Integration.DoblesDePrueba</c> ni ningún tipo de la aplicación que termine en <c>DePrueba</c>;</item>
/// <item>la API no publica las rutas <c>/api/lending/test-doubles</c> ni lee la bandera <c>Lending:TestDoubles:Enabled</c>, y registra siempre los
/// consumidores reales.</item>
/// </list>
/// </summary>
public class NoHayDoblesDePrueba
{
    /// <summary>Los consumidores de mensajes admitidos en Recaudo y Tesorería, por nombre completo.</summary>
    private static readonly string[] Permitidos =
    [
        // Fase 7 (T0132): el único consumidor real del destino Treasury.
        "IngenIA365ERP.Application.Treasury.Integration.DestinoTesoreria",
        // Fase 10 (T0176): el único consumidor real del destino Receipts.
        "IngenIA365ERP.Application.Receipts.Integration.DestinoRecaudo",
    ];

    private static IEnumerable<Type> TiposDeLaAplicacion() => typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes();

    [Fact]
    public void Solo_los_consumidores_del_modulo_implementan_los_destinos_de_Recaudo_y_Tesoreria()
    {
        var infractores = TiposDeLaAplicacion()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDestinoDeMensajes).IsAssignableFrom(t))
            .Where(t => RecaudoYTesoreria.EsDeRecaudo(t.Namespace) || RecaudoYTesoreria.EsDeTesoreria(t.Namespace)
                        || t.Name.Contains("DePrueba", StringComparison.Ordinal) || (t.Namespace ?? string.Empty).Contains("DoblesDePrueba", StringComparison.Ordinal))
            .Where(t => !Permitidos.Contains(t.FullName, StringComparer.Ordinal))
            .Select(t => t.FullName!)
            .ToList();
        ReglaSobreFuente.SinInfracciones(infractores, "Un consumidor no admitido de los destinos de Recaudo o Tesorería, o un doble de prueba (FR-223)");
    }

    [Fact]
    public void No_queda_ningun_doble_de_prueba_ni_su_espacio()
    {
        var dobles = TiposDeLaAplicacion()
            .Where(t => !t.Name.Contains('<', StringComparison.Ordinal))
            .Where(t => (t.IsClass && t.Name.EndsWith("DePrueba", StringComparison.Ordinal))
                        || (t.Namespace ?? string.Empty).EndsWith(".DoblesDePrueba", StringComparison.Ordinal))
            .Select(t => t.FullName!)
            .ToList();
        ReglaSobreFuente.SinInfracciones(dobles, "Un doble de prueba en la aplicación (FR-223, TR32; retirados en T0307)");
    }

    [Fact]
    public void La_API_no_publica_las_rutas_de_los_dobles_ni_lee_su_bandera_y_registra_los_consumidores_reales()
    {
        // La única mención admitida: la lista de bajas del sembrador de permisos, que da de baja Lending.TestDoubles.Operate donde ya existía.
        var fuentes = RepoPath.ProductionCSharpFiles()
            .Where(f => !f.Replace('\\', '/').EndsWith("Identity/Seed/LendingPermissionCatalogSeeder.cs", StringComparison.Ordinal))
            .Select(f => (Archivo: f, Texto: FuenteSinComentarios.Leer(f)))
            .Where(x => x.Texto.Contains("test-doubles", StringComparison.Ordinal) || x.Texto.Contains("Lending:TestDoubles", StringComparison.Ordinal)
                        || x.Texto.Contains("TestDoubles.Operate", StringComparison.Ordinal))
            .Select(x => x.Archivo)
            .ToList();
        ReglaSobreFuente.SinInfracciones(fuentes, "Una ruta, una bandera o un permiso de los dobles de prueba en el código (T0307)");

        var programa = FuenteSinComentarios.Leer(ReglaSobreFuente.ArchivosNombrados(["Presentation/IngenIA365ERP.API/Program.cs"]).Single());
        Assert.Contains("ConsumidoresDeRecaudoYTesoreria.Registrar(builder.Services)", programa, StringComparison.Ordinal);
    }
}
