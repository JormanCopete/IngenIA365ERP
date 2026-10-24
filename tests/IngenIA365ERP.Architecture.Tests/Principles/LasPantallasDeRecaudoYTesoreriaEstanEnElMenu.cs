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
        // Fase 13 (T0219): los cheques recibidos.
        ("Pages/Recaudo/ChequesRecibidos.razor", "/recaudo/cheques-recibidos", "Receipts.Checks.View"),
        // Fase 14 (T0230): chequeras, cheques girados y remesas.
        ("Pages/Tesoreria/Chequeras.razor", "/tesoreria/chequeras", "Treasury.Checkbooks.View"),
        ("Pages/Tesoreria/ChequesGirados.razor", "/tesoreria/cheques-girados", "Treasury.Checks.View"),
        ("Pages/Tesoreria/Remesas.razor", "/tesoreria/remesas", "Treasury.Remittances.View"),
        // Fase 15 (T0245): el cumplimiento y la contingencia.
        ("Pages/Recaudo/Cumplimiento.razor", "/recaudo/cumplimiento", "Receipts.Compliance.View"),
        ("Pages/Recaudo/Contingencia.razor", "/recaudo/contingencia", "Receipts.Receipt.RegisterContingency"),
        // Fase 16 (T0266): los lotes de Recaudo en Contabilidad (la matriz, sus tipos y su completitud son páginas delgadas sobre componentes con su indicador).
        ("Pages/Contabilidad/Recaudo/Lotes.razor", "/contabilidad/recaudo/lotes", "Accounting.ReceiptsMatrix.View"),
        // Fase 17 (T0280): la conciliación bancaria mínima.
        ("Pages/Tesoreria/Conciliacion.razor", "/tesoreria/conciliacion", "Treasury.Reconciliation.View"),
        // Fase 19 (T0304): la activación completa de cada módulo.
        ("Pages/Recaudo/Activacion.razor", "/recaudo/activacion", "Receipts.Activation.View"),
        ("Pages/Tesoreria/Activacion.razor", "/tesoreria/activacion", "Treasury.Activation.View"),
        // Fases 5 a 10 (llenadas en la fase 20, T0316): la caja, las sesiones, el cierre, la bóveda, los recibos, las anulaciones, los reversos y los saldos a favor.
        ("Pages/Recaudo/Caja.razor", "/recaudo/caja", "Receipts.Receipt.Register"),
        ("Pages/Recaudo/Recibos.razor", "/recaudo/recibos", "Receipts.Receipt.View"),
        ("Pages/Recaudo/Sesiones.razor", "/recaudo/sesiones", "Receipts.Session.Open"),
        ("Pages/Recaudo/CierreDelDia.razor", "/recaudo/cierre-del-dia", "Receipts.Session.View"),
        ("Pages/Recaudo/Boveda.razor", "/recaudo/boveda", "Receipts.Vault.View"),
        ("Pages/Recaudo/Anulaciones.razor", "/recaudo/anulaciones", "Receipts.Voiding.Request"),
        ("Pages/Recaudo/ReversosDeModulos.razor", "/recaudo/reversos-de-modulos", "Receipts.Reversals.View"),
        ("Pages/Recaudo/SaldosAFavor.razor", "/recaudo/saldos-a-favor", "Receipts.CreditBalances.View"),
        // Fases 7 a 9 (llenadas en la fase 20): las órdenes, los lotes, los archivos, los resultados, las cuentas propias y el fondo de liquidez.
        ("Pages/Tesoreria/Ordenes.razor", "/tesoreria/ordenes", "Treasury.Orders.View"),
        ("Pages/Tesoreria/Lotes.razor", "/tesoreria/lotes", "Treasury.Batches.Prepare"),
        ("Pages/Tesoreria/ArchivosDePago.razor", "/tesoreria/archivos-de-pago", "Treasury.PaymentFiles.Download"),
        ("Pages/Tesoreria/Resultados.razor", "/tesoreria/resultados", "Treasury.Results.Register"),
        ("Pages/Tesoreria/CuentasBancarias.razor", "/tesoreria/cuentas-bancarias", "Treasury.BankAccounts.View"),
        ("Pages/Tesoreria/FondoDeLiquidez.razor", "/tesoreria/fondo-de-liquidez", "Treasury.LiquidityFund.Request"),
        // Fase 20 (T0316): los centros de informes.
        ("Pages/Recaudo/Informes.razor", "/recaudo/informes", "Receipts.Reports.View"),
        ("Pages/Tesoreria/Informes.razor", "/tesoreria/informes", "Treasury.Reports.View"),
        // Fase 21 (T0322): la lista de go/no-go de la salida en vivo, en Administración.
        ("Pages/Administracion/GoNoGo.razor", "/admin/go-no-go", "Platform.GoLive.View"),
        // R1 (fases 25 a 31): las pantallas de la entrega R1.
        ("Pages/Recaudo/RecaudoPendienteDeAplicar.razor", "/recaudo/pendiente-de-aplicar", "Receipts.PendingApplications.View"),
        // R1, US4 (fase 26): los egresos de caja.
        ("Pages/Recaudo/Egresos.razor", "/recaudo/egresos", "Receipts.Disbursements.View"),
        // R1, US6 (fase 27): las reglas de identificación y la consignación reportada.
        ("Pages/Recaudo/ReglasDeIdentificacion.razor", "/recaudo/reglas-de-identificacion", "Receipts.Setup.View"),
        ("Pages/Recaudo/ConsignacionesReportadas.razor", "/recaudo/consignaciones-reportadas", "Receipts.Receipt.Register"),
        // R1, US21 (fase 31): el tablero de caja.
        ("Pages/Recaudo/Tablero.razor", "/recaudo/tablero", "Receipts.Reports.View"),
        // Fase 18 (T0290): /recaudo/puesta-en-marcha y /tesoreria/puesta-en-marcha son páginas delgadas sobre Components/Recaudo/PuestaEnMarchaDeModulo, que lleva
        // un IndicadorDeCarga por zona (como las de la matriz de la fase 16); su enlace en el menú lo comprueba Las_puestas_en_marcha_estan_en_el_menu.
    ];

    [Fact]
    public void Las_puestas_en_marcha_estan_en_el_menu()
    {
        var menu = File.ReadAllText(Path.Combine(Shared, "Layout", "NavMenu.razor"));
        var componente = File.ReadAllText(Path.Combine(Shared, "Components", "Recaudo", "PuestaEnMarchaDeModulo.razor"));
        var infracciones = new List<string>();
        foreach (var (archivo, ruta) in new[] { ("Pages/Recaudo/PuestaEnMarcha.razor", "/recaudo/puesta-en-marcha"), ("Pages/Tesoreria/PuestaEnMarcha.razor", "/tesoreria/puesta-en-marcha") })
        {
            var camino = Path.Combine(Shared, archivo.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(camino)) { infracciones.Add($"{archivo}: no existe"); continue; }
            var texto = File.ReadAllText(camino);
            if (!texto.Contains($"@page \"{ruta}\"", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin @page {ruta}");
            if (!texto.Contains("<PuestaEnMarchaDeModulo", StringComparison.Ordinal)) infracciones.Add($"{archivo}: no usa PuestaEnMarchaDeModulo");
            if (!menu.Contains($"href=\"{ruta}\"", StringComparison.Ordinal)) infracciones.Add($"{ruta}: el menú no la enlaza");
        }
        if (!componente.Contains("<IndicadorDeCarga", StringComparison.Ordinal)) infracciones.Add("PuestaEnMarchaDeModulo: sin IndicadorDeCarga");
        ReglaSobreFuente.SinInfracciones(infracciones, "Puestas en marcha de Recaudo o Tesorería fuera del contrato (FR-018, FR-162)");
    }

    /// <summary>
    /// Fase 20 (T0316): las páginas delgadas de la matriz contable de los dos módulos (fase 16) delegan en un componente de <c>Components/Contabilidad</c> que lleva su
    /// <c>IndicadorDeCarga</c>; aquí se exige que existan con su <c>@page</c> y su permiso y que el menú las enlace.
    /// </summary>
    private static readonly (string Archivo, string Ruta, string Permiso)[] Delgadas =
    [
        ("Pages/Contabilidad/Recaudo/Matriz.razor", "/contabilidad/recaudo/matriz", "Accounting.ReceiptsMatrix.View"),
        ("Pages/Contabilidad/Recaudo/TiposDeComprobante.razor", "/contabilidad/recaudo/tipos-de-comprobante", "Accounting.ReceiptsMatrix.View"),
        ("Pages/Contabilidad/Recaudo/Completitud.razor", "/contabilidad/recaudo/completitud", "Accounting.ReceiptsMatrix.View"),
        ("Pages/Contabilidad/Tesoreria/Matriz.razor", "/contabilidad/tesoreria/matriz", "Accounting.TreasuryMatrix.View"),
        ("Pages/Contabilidad/Tesoreria/TiposDeComprobante.razor", "/contabilidad/tesoreria/tipos-de-comprobante", "Accounting.TreasuryMatrix.View"),
        ("Pages/Contabilidad/Tesoreria/Completitud.razor", "/contabilidad/tesoreria/completitud", "Accounting.TreasuryMatrix.View"),
    ];

    [Fact]
    public void Las_paginas_delgadas_de_la_matriz_existen_protegidas_y_en_el_menu()
    {
        var menu = File.ReadAllText(Path.Combine(Shared, "Layout", "NavMenu.razor"));
        var infracciones = new List<string>();
        foreach (var (archivo, ruta, permiso) in Delgadas)
        {
            var camino = Path.Combine(Shared, archivo.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(camino)) { infracciones.Add($"{archivo}: no existe"); continue; }
            var texto = File.ReadAllText(camino);
            if (!texto.Contains($"@page \"{ruta}\"", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin @page {ruta}");
            if (!texto.Contains($"Required=\"{permiso}\"", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin permiso {permiso}");
            if (!menu.Contains($"href=\"{ruta}\"", StringComparison.Ordinal)) infracciones.Add($"{ruta}: el menú no la enlaza");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Páginas de la matriz de Recaudo o Tesorería fuera del contrato (FR-018, FR-162)");
    }

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
