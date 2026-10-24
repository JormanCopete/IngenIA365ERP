using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0004 (R-35; FR-018, FR-162; contracts/api.md §18): cada pantalla de Recaudo y de la Tesorería nueva existe con su
/// <c>@page</c>, detrás de su permiso, con <c>IndicadorDeCarga</c>, y el menú la enlaza (los grupos «Recaudo» y «Tesorería» nacen con su
/// primera página; lo heredado queda en «Tesorería · heredada»). Las páginas de detalle, a las que se llega desde otra pantalla, van en
/// <see cref="Detalles"/>: no van al menú. Molde: <see cref="LasPantallasDeCarteraEstanEnElMenu"/>.
///
/// <para>
/// Árbol que recorre: <c>Shared/Pages/Recaudo</c>, <c>Shared/Pages/Tesoreria</c> (sin las cuatro heredadas) y las de plataforma de la 015.
/// Las listas nacen vacías en el Setup; la fase 2 (T0032) suma la de cuentas bancarias de la persona como detalle, y cada historia suma las
/// suyas.
/// </para>
/// </summary>
public class LasPantallasDeRecaudoYTesoreriaEstanEnElMenu
{
    private static readonly string Shared = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared");

    /// <summary>Pantallas del menú: archivo (relativo a Shared), ruta y permiso.</summary>
    private static readonly (string Archivo, string Ruta, string Permiso)[] Pantallas =
    [
        // Fases 3 y 4 (T0063, T0077): el alcance y la configuración de Recaudo.
        ("Pages/Recaudo/Configuracion/Puntos.razor", "/recaudo/configuracion/puntos", "Receipts.Setup.View"),
        ("Pages/Recaudo/Configuracion/Cajas.razor", "/recaudo/configuracion/cajas", "Receipts.Setup.View"),
        ("Pages/Recaudo/Configuracion/Medios.razor", "/recaudo/configuracion/medios", "Receipts.Setup.View"),
        ("Pages/Recaudo/Configuracion/Bovedas.razor", "/recaudo/configuracion/bovedas", "Receipts.Setup.View"),
        ("Pages/Recaudo/Configuracion/TiposDeDocumento.razor", "/recaudo/configuracion/tipos-de-documento", "Receipts.Setup.View"),
        ("Pages/Recaudo/Configuracion/Conceptos.razor", "/recaudo/configuracion/conceptos", "Receipts.Setup.View"),
        ("Pages/Recaudo/Configuracion/Talonarios.razor", "/recaudo/configuracion/talonarios", "Receipts.Setup.View"),
        ("Pages/Recaudo/Configuracion/Suplencias.razor", "/recaudo/configuracion/suplencias", "Receipts.Setup.View"),
        ("Pages/Recaudo/Configuracion/Alcance.razor", "/recaudo/configuracion/alcance", "Receipts.Scope.Manage"),
        // Fase 11 (T0199): los extractos, los abonos y las partidas.
        ("Pages/Recaudo/Extractos.razor", "/recaudo/extractos", "Receipts.BankItems.View"),
        ("Pages/Recaudo/Partidas.razor", "/recaudo/partidas", "Receipts.UnidentifiedItems.View"),
        ("Pages/Tesoreria/Extractos.razor", "/tesoreria/extractos", "Treasury.Statements.View"),
        // Fase 12 (T0210): las pagadurías.
        ("Pages/Recaudo/Pagadurias.razor", "/recaudo/pagadurias", "Receipts.Paymasters.View"),
    ];

    /// <summary>Páginas de detalle (con parámetro de ruta): archivo, la ruta tal como la declara <c>@page</c> y permiso.</summary>
    private static readonly (string Archivo, string Ruta, string Permiso)[] Detalles =
    [
        // Fase 2 (T0032): las cuentas bancarias de la persona, a las que se llega desde PersonaDialog.
        ("Pages/Maestros/CuentasBancariasDePersona.razor", "/maestros/personas/{PersonaPublicId:guid}/cuentas-bancarias", "Core.PersonBankAccounts.View"),
    ];

    [Fact]
    public void Cada_pantalla_existe_protegida_con_indicador_y_en_el_menu()
    {
        var menu = File.ReadAllText(Path.Combine(Shared, "Layout", "NavMenu.razor"));
        var infracciones = Revisar(Pantallas);
        infracciones.AddRange(Pantallas.Where(p => !menu.Contains($"href=\"{p.Ruta}\"", StringComparison.Ordinal)).Select(p => $"{p.Ruta}: el menú no la enlaza"));
        ReglaSobreFuente.SinInfracciones(infracciones, "Pantallas de Recaudo o Tesorería fuera del contrato (FR-018, FR-162)");
    }

    [Fact]
    public void Cada_detalle_existe_protegido_y_con_indicador() =>
        ReglaSobreFuente.SinInfracciones(Revisar(Detalles), "Detalles de Recaudo o Tesorería fuera del contrato (FR-018)");

    private static List<string> Revisar(IEnumerable<(string Archivo, string Ruta, string Permiso)> paginas)
    {
        var infracciones = new List<string>();
        foreach (var (archivo, ruta, permiso) in paginas)
        {
            var camino = Path.Combine(Shared, archivo.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(camino)) { infracciones.Add($"{archivo}: no existe"); continue; }
            var texto = File.ReadAllText(camino);
            if (!texto.Contains($"@page \"{ruta}\"", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin @page {ruta}");
            if (!texto.Contains($"Required=\"{permiso}\"", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin permiso {permiso}");
            if (!texto.Contains("<IndicadorDeCarga", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin IndicadorDeCarga");
        }
        return infracciones;
    }
}
