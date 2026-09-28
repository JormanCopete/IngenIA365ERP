using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, I4 (T753–T759; decisiones-transversales §2.11): las cinco pantallas de facturación electrónica existen con su ruta, se
/// protegen con el permiso de su área (<c>PermissionGate</c>), dicen que están cargando (<c>IndicadorDeCarga</c>, también en
/// Administración, que no está entre los módulos migrados) y tienen su enlace en el menú detrás del mismo permiso. Ninguna nombra al
/// sistema anterior. Los componentes nuevos del detalle de venta y del POS se usan donde la tarea dice. (nuevo)
/// </summary>
public class LasPantallasDeFacturacionElectronicaEstanEnElMenu
{
    private static readonly string Shared = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared");

    public static TheoryData<string, string, string> Pantallas => new()
    {
        { "Pages/Administracion/FacturacionElectronica.razor", "/admin/facturacion-electronica", "ElectronicInvoicing.Settings.View" },
        { "Pages/Maestros/ResolucionesDian.razor", "/maestros/resoluciones-dian", "ElectronicInvoicing.Resolutions.View" },
        { "Pages/Ventas/DocumentosElectronicos.razor", "/ventas/documentos-electronicos", "ElectronicInvoicing.Documents.View" },
        { "Pages/Ventas/ContingenciasDian.razor", "/ventas/contingencias-dian", "ElectronicInvoicing.Contingencies.View" },
        { "Pages/Compras/DocumentosSoporte.razor", "/compras/documentos-soporte", "Inventory.Purchases.View" },
    };

    [Theory]
    [MemberData(nameof(Pantallas))]
    public void La_pantalla_existe_protegida_y_con_indicador(string archivo, string ruta, string permiso)
    {
        var camino = Path.Combine(Shared, archivo.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(camino), $"Falta {archivo}.");
        var texto = File.ReadAllText(camino);
        Assert.Contains($"@page \"{ruta}\"", texto, StringComparison.Ordinal);
        Assert.Contains($"<PermissionGate Required=\"{permiso}\" MostrarAviso=\"true\"", texto, StringComparison.Ordinal);
        Assert.Contains("<IndicadorDeCarga", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("SOLIDO", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(Pantallas))]
    public void El_menu_la_enlaza_detras_de_su_permiso(string archivo, string ruta, string permiso)
    {
        _ = archivo;
        var menu = File.ReadAllText(Path.Combine(Shared, "Layout", "NavMenu.razor"));
        var enlace = Regex.Match(menu,
            $@"<PermissionGate Required=""{Regex.Escape(permiso)}"">\s*(?:<NavLink[^>]*>[^<]*</NavLink>\s*)*<NavLink href=""{Regex.Escape(ruta)}""",
            RegexOptions.Singleline);
        Assert.True(enlace.Success, $"El menú no enlaza {ruta} dentro de <PermissionGate Required=\"{permiso}\">.");
    }

    [Fact]
    public void El_detalle_de_venta_y_el_POS_usan_los_componentes_nuevos()
    {
        var detalle = File.ReadAllText(Path.Combine(Shared, "Pages", "Ventas", "Documento.razor"));
        var pos = File.ReadAllText(Path.Combine(Shared, "Pages", "Pos", "PuntoDeVenta.razor"));
        Assert.Contains("<EstadoElectronico", detalle, StringComparison.Ordinal);
        Assert.Contains("PedirFacturaAsync", detalle, StringComparison.Ordinal);
        Assert.Contains("<PendientesDeEntrega", pos, StringComparison.Ordinal);
    }

    [Fact]
    public void Las_contingencias_adjuntan_sus_constancias_al_evento()
    {
        var texto = File.ReadAllText(Path.Combine(Shared, "Pages", "Ventas", "ContingenciasDian.razor"));
        Assert.Contains("OwnerEntityType=\"DianContingencyEvent\"", texto, StringComparison.Ordinal);
    }
}
