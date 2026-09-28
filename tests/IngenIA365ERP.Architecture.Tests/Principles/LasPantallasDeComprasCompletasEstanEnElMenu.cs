using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, I5 (T811–T816, T847–T849): las cuatro pantallas de compras completas existen con su ruta, se protegen con
/// <c>Inventory.Purchases.View</c> (<c>PermissionGate</c>), dicen que están cargando (<c>IndicadorDeCarga</c>), no nombran al sistema
/// anterior y tienen su enlace en el grupo Compras del menú detrás del mismo permiso. Las pantallas de US9 suman lo de I5 (la recepción
/// contra orden, la pestaña del cruce y la emisión RADIAN), el impacto en costos está en las cuatro pantallas que la tarea nombra, el
/// cambio de método se hace desde Parámetros por su diálogo, el kardex muestra las capas consumidas y los informes ofrecen los filtros
/// propios de las vistas nuevas. (nuevo)
/// </summary>
public class LasPantallasDeComprasCompletasEstanEnElMenu
{
    private static readonly string Shared = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared");

    private static string Leer(params string[] partes) => File.ReadAllText(Path.Combine([Shared, .. partes]));

    public static TheoryData<string, string> Pantallas => new()
    {
        { "Pages/Compras/Solicitudes.razor", "/compras/solicitudes" },
        { "Pages/Compras/Ordenes.razor", "/compras/ordenes" },
        { "Pages/Compras/Cruce.razor", "/compras/cruce" },
        { "Pages/Compras/CostosAdicionales.razor", "/compras/costos-adicionales" },
    };

    [Theory]
    [MemberData(nameof(Pantallas))]
    public void La_pantalla_existe_protegida_y_con_indicador(string archivo, string ruta)
    {
        var camino = Path.Combine(Shared, archivo.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(camino), $"Falta {archivo}.");
        var texto = File.ReadAllText(camino);
        Assert.Contains($"@page \"{ruta}\"", texto, StringComparison.Ordinal);
        Assert.Contains("<PermissionGate Required=\"Inventory.Purchases.View\" MostrarAviso=\"true\"", texto, StringComparison.Ordinal);
        Assert.Contains("<IndicadorDeCarga", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("SOLIDO", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Headers.Authorization", texto, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Pantallas))]
    public void El_menu_la_enlaza_detras_de_su_permiso(string archivo, string ruta)
    {
        _ = archivo;
        var menu = Leer("Layout", "NavMenu.razor");
        var enlace = Regex.Match(menu,
            $@"<PermissionGate Required=""Inventory\.Purchases\.View"">\s*(?:<NavLink[^>]*>[^<]*</NavLink>\s*)*<NavLink href=""{Regex.Escape(ruta)}""",
            RegexOptions.Singleline);
        Assert.True(enlace.Success, $"El menú no enlaza {ruta} dentro de <PermissionGate Required=\"Inventory.Purchases.View\">.");
    }

    [Fact]
    public void Las_pantallas_de_US9_suman_lo_de_I5()
    {
        var recepcion = Leer("Pages", "Compras", "Recepcion.razor");
        Assert.Contains("OrderLinePublicId", recepcion, StringComparison.Ordinal);
        Assert.Contains("SupplyParameterFromQuery(Name = \"order\")", recepcion, StringComparison.Ordinal);

        var factura = Leer("Pages", "Compras", "FacturaProveedor.razor");
        Assert.Contains("Cruce", factura, StringComparison.Ordinal);
        Assert.Contains("<PermissionGate Required=\"Inventory.Purchases.EmitRadianEvent\">", factura, StringComparison.Ordinal);
        Assert.Contains("EmitirEventosRadianAsync", factura, StringComparison.Ordinal);

        var ordenes = Leer("Pages", "Compras", "Ordenes.razor");
        Assert.Contains("/compras/recepciones/nueva?order=", ordenes, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Pages/Compras/Recepcion.razor")]
    [InlineData("Pages/Compras/FacturaProveedor.razor")]
    [InlineData("Pages/Inventario/AjusteDetalle.razor")]
    [InlineData("Pages/Compras/Devoluciones.razor")]
    public void El_impacto_en_costos_se_ve_antes_de_confirmar(string archivo)
    {
        var texto = File.ReadAllText(Path.Combine(Shared, archivo.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("<ImpactoEnCostos", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void El_componente_de_impacto_exige_ver_costos_y_dice_que_esta_cargando()
    {
        var texto = Leer("Components", "Inventario", "ImpactoEnCostos.razor");
        Assert.Contains("<PermissionGate Required=\"Inventory.Costs.Read\"", texto, StringComparison.Ordinal);
        Assert.Contains("<IndicadorDeCarga", texto, StringComparison.Ordinal);
        Assert.Contains("Ver impacto en costos", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void El_cambio_de_metodo_va_por_su_dialogo_desde_Parametros()
    {
        var dialogo = Leer("Components", "Inventario", "CambioDeMetodoDialog.razor");
        Assert.Contains("<PermissionGate Required=\"Inventory.Costing.Manage\"", dialogo, StringComparison.Ordinal);
        Assert.Contains("<IndicadorDeCarga", dialogo, StringComparison.Ordinal);
        Assert.Contains("/inventario/informes?vista=method-change-valuation", dialogo, StringComparison.Ordinal);
        Assert.Contains("<CambioDeMetodoDialog", Leer("Pages", "Inventario", "Parametros.razor"), StringComparison.Ordinal);
    }

    [Fact]
    public void El_kardex_muestra_las_capas_y_los_informes_los_filtros_de_las_vistas_nuevas()
    {
        Assert.Contains("Capas consumidas", Leer("Pages", "Inventario", "Kardex.razor"), StringComparison.Ordinal);
        var informes = Leer("Pages", "Inventario", "Informes.razor");
        Assert.Contains("comparativeFrom", informes, StringComparison.Ordinal);
        Assert.Contains("EstadosDelCruce", informes, StringComparison.Ordinal);
    }
}
