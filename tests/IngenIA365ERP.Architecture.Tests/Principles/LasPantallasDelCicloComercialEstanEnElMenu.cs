using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, I6 (T891–T901; contracts/api.md §18.4, §19.4; decisiones-transversales §2.11): las cinco pantallas del ciclo comercial
/// —cotizaciones, pedidos, remisiones, notas débito y promociones— existen con su ruta, se protegen con su permiso (<c>PermissionGate</c>),
/// dicen que están cargando (<c>IndicadorDeCarga</c>), no nombran al sistema anterior ni ponen <c>Authorization</c> y tienen su enlace en el
/// grupo Ventas del menú detrás del mismo permiso. La factura de oficina elige su origen (pedido o remisiones), el detalle de la venta y
/// la línea del POS dicen qué promoción se aplicó, las rutas de I6 viven en <c>SalesEndpoints</c> con su permiso y su clave, y el manual de
/// la app tiene un tema para cada pantalla. (nuevo)
/// </summary>
public class LasPantallasDelCicloComercialEstanEnElMenu
{
    private static readonly string Raiz = RepoPath.FindRepoRoot();
    private static readonly string Shared = Path.Combine(Raiz, "src", "Presentation", "IngenIA365ERP.Shared");

    private static string Leer(params string[] partes) => File.ReadAllText(Path.Combine([Shared, .. partes]));

    public static TheoryData<string, string, string> Pantallas => new()
    {
        { "Pages/Ventas/Cotizaciones.razor", "/ventas/cotizaciones", "Inventory.Sales.View" },
        { "Pages/Ventas/Pedidos.razor", "/ventas/pedidos", "Inventory.Sales.View" },
        { "Pages/Ventas/Remisiones.razor", "/ventas/remisiones", "Inventory.Sales.View" },
        { "Pages/Ventas/NotasDebito.razor", "/ventas/notas-debito", "Inventory.Sales.View" },
        { "Pages/Ventas/Promociones.razor", "/ventas/promociones", "Inventory.Prices.View" },
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
        Assert.Contains("_carga.Iniciar()", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("SOLIDO", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Headers.Authorization", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("<style", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(Pantallas))]
    public void El_menu_la_enlaza_detras_de_su_permiso(string archivo, string ruta, string permiso)
    {
        _ = archivo;
        var menu = Leer("Layout", "NavMenu.razor");
        var enlace = Regex.Match(menu,
            $@"<PermissionGate Required=""{Regex.Escape(permiso)}"">\s*(?:<NavLink[^>]*>[^<]*</NavLink>\s*)*<NavLink href=""{Regex.Escape(ruta)}""",
            RegexOptions.Singleline);
        Assert.True(enlace.Success, $"El menú no enlaza {ruta} dentro de <PermissionGate Required=\"{permiso}\">.");
    }

    [Fact]
    public void Las_acciones_de_cada_pantalla_estan_detras_de_su_permiso()
    {
        // El borrador de los cuatro documentos es un solo componente: guardar, confirmar, anular y descartar detrás de su permiso.
        var editor = Leer("Components", "Ventas", "EditorDelCiclo.razor");
        Assert.Contains("<PermissionGate Required=\"Inventory.Sales.Create\">", editor, StringComparison.Ordinal);
        Assert.Contains("<PermissionGate Required=\"Inventory.Sales.Confirm\">", editor, StringComparison.Ordinal);
        Assert.Contains("<PermissionGate Required=\"Inventory.Sales.Void\">", editor, StringComparison.Ordinal);
        Assert.Contains("<IndicadorDeCarga", editor, StringComparison.Ordinal);
        Assert.Contains("ExistenciaDelProductoAsync", editor, StringComparison.Ordinal);
        Assert.Contains("<PanelDeCobro", editor, StringComparison.Ordinal);
        Assert.Contains("FormularioDelCiclo.Promociones", editor, StringComparison.Ordinal);
        foreach (var (archivo, ruta) in new[] { ("Cotizaciones.razor", "Cotizaciones"), ("Pedidos.razor", "Pedidos"), ("Remisiones.razor", "Remisiones"), ("NotasDebito.razor", "NotasDebito") })
            Assert.Contains($"<EditorDelCiclo @key=\"_clave\" Ruta=\"@VentasClient.Rutas.{ruta}\"", Leer("Pages", "Ventas", archivo), StringComparison.Ordinal);

        Assert.Contains("ConvertirEnPedidoAsync", Leer("Pages", "Ventas", "Cotizaciones.razor"), StringComparison.Ordinal);
        var pedidos = Leer("Pages", "Ventas", "Pedidos.razor");
        Assert.Contains("ConExistencias=\"true\"", pedidos, StringComparison.Ordinal);
        Assert.Contains("/ventas/facturas/nueva?pedido=", pedidos, StringComparison.Ordinal);
        Assert.Contains("/ventas/remisiones?pedido=", pedidos, StringComparison.Ordinal);
        Assert.Contains("/ventas/facturas/nueva?remisiones=", Leer("Pages", "Ventas", "Remisiones.razor"), StringComparison.Ordinal);
        var notas = Leer("Pages", "Ventas", "NotasDebito.razor");
        Assert.Contains("<EstadoElectronico", notas, StringComparison.Ordinal);
        Assert.Contains("ConPagos=\"true\"", notas, StringComparison.Ordinal);
        var promociones = Leer("Pages", "Ventas", "Promociones.razor");
        Assert.Contains("<PermissionGate Required=\"Inventory.Prices.Manage\"", promociones, StringComparison.Ordinal);
        Assert.Contains("<CampoCodigo", promociones, StringComparison.Ordinal);
        Assert.Contains("Catalogo=\"promociones\"", promociones, StringComparison.Ordinal);
    }

    [Fact]
    public void La_factura_de_oficina_elige_su_origen()
    {
        var factura = Leer("Pages", "Ventas", "NuevaFactura.razor");
        Assert.Contains("SupplyParameterFromQuery(Name = \"pedido\")", factura, StringComparison.Ordinal);
        Assert.Contains("SupplyParameterFromQuery(Name = \"remisiones\")", factura, StringComparison.Ordinal);
        Assert.Contains("OriginPublicIds", factura, StringComparison.Ordinal);
        Assert.Contains("FormularioDelCiclo.Pedido(_lineas)", factura, StringComparison.Ordinal); // reenvía la línea origen
        Assert.Contains("Inventory.PostingMode.ChainMismatch", factura, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Pages/Ventas/Documento.razor")]
    [InlineData("Components/Pos/LineasDeVenta.razor")]
    public void El_documento_dice_que_promocion_se_aplico(string archivo)
    {
        var texto = File.ReadAllText(Path.Combine(Shared, archivo.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("PromotionName", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Las_rutas_de_I6_viven_en_SalesEndpoints_con_su_permiso_y_su_clave()
    {
        var fuente = File.ReadAllText(Path.Combine(Raiz, "src", "Presentation", "IngenIA365ERP.API", "Endpoints", "Inventory", "SalesEndpoints.cs"));
        foreach (var prefijo in new[] { "/api/inventory/sales/quotes", "/api/inventory/sales/orders", "/api/inventory/sales/shipments",
                     "/api/inventory/sales/debit-notes", "/api/inventory/promotions" })
            Assert.Contains($"\"{prefijo}\"", fuente, StringComparison.Ordinal);
        Assert.Contains("\"/{id:guid}/to-order\"", fuente, StringComparison.Ordinal);
        Assert.Contains("ConvertQuoteToOrderCommand", fuente, StringComparison.Ordinal);
        Assert.Contains("RutasDeVenta.Cotizaciones", fuente, StringComparison.Ordinal);
        Assert.Contains("RutasDeVenta.NotasDebito", fuente, StringComparison.Ordinal);
        Assert.Contains("CreatePromotionCommand", fuente, StringComparison.Ordinal);
        Assert.Contains("UpdatePromotionCommand", fuente, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/ventas/cotizaciones")]
    [InlineData("/ventas/pedidos")]
    [InlineData("/ventas/remisiones")]
    [InlineData("/ventas/notas-debito")]
    [InlineData("/ventas/promociones")]
    public void El_manual_de_la_app_tiene_su_tema(string ruta)
    {
        var catalogo = Leer("Services", "Manual", "ManualCatalogo.cs");
        Assert.Matches(new Regex($@"Proceso\(""[a-z0-9-]+"",\s*""[^""]+"",\s*Modulos\.Ventas,\s*""{Regex.Escape(ruta)}"""), catalogo);
    }
}
