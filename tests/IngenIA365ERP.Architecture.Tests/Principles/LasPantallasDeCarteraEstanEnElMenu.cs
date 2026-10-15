using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.16, T98): cada pantalla de Cartera existe con su <c>@page</c>, detrás de su permiso, con
/// <c>IndicadorDeCarga</c>, y el menú la enlaza. Molde: <c>LasPantallasDelCicloComercialEstanEnElMenu</c>.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0007) y la llena cada historia con sus pantallas; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LasPantallasDeCarteraEstanEnElMenu
{
    private static readonly string Shared = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared");

    /// <summary>Pantallas: archivo (relativo a Shared), ruta y permiso.</summary>
    /// <para>Fase 4, US17 (T0324, T0310 y el pendiente de T0273): las diez pantallas de gobierno, configuración y mensajes al deudor.</para>
    /// <para>Fase 5, US1 (T0362): las líneas de crédito.</para>
    private static readonly (string Archivo, string Ruta, string Permiso)[] Pantallas =
    [
        ("Pages/Cartera/Atribuciones.razor", "/cartera/atribuciones", "Lending.Attributions.View"),
        ("Pages/Cartera/Actas.razor", "/cartera/actas", "Lending.Sessions.Read"),
        ("Pages/Cartera/Vinculados.razor", "/cartera/vinculados", "Lending.RelatedParties.Read"),
        ("Pages/Cartera/Parametros.razor", "/cartera/parametros", "Lending.Parameters.View"),
        ("Pages/Cartera/Plantillas.razor", "/cartera/plantillas", "Lending.Templates.View"),
        ("Pages/Cartera/Alertas.razor", "/cartera/alertas", "Lending.Alerts.View"),
        ("Pages/Cartera/Autorizaciones.razor", "/cartera/autorizaciones", "Compliance.HabeasData.ViewHistory"),
        ("Pages/Cartera/Alcances.razor", "/cartera/alcances", "Lending.Scopes.Manage"),
        ("Pages/Cartera/Mensajes.razor", "/cartera/mensajes", "Lending.DebtorMessages.Read"),
        ("Pages/Cartera/PlantillasDeMensaje.razor", "/cartera/plantillas-de-mensaje", "Lending.MessageTemplates.View"),
        // Fase 5, US1 (T0360, T0362): las líneas de crédito con sus equivalencias de Comercio. Su detalle (/cartera/lineas/{id:guid}, T0361)
        // se abre desde la lista, sin enlace propio: se comprueba aparte.
        ("Pages/Cartera/Lineas.razor", "/cartera/lineas", "Lending.CreditLines.View"),
        // Fase 6, US2 (T0414): el simulador, las solicitudes, «Mis pendientes» y los cupos.
        ("Pages/Cartera/Simulador.razor", "/cartera/simulador", "Lending.Simulations.Run"),
        ("Pages/Cartera/Solicitudes.razor", "/cartera/solicitudes", "Lending.Applications.Read"),
        ("Pages/Cartera/Aprobaciones.razor", "/cartera/aprobaciones", "Lending.Applications.Read"),
        ("Pages/Cartera/Cupos.razor", "/cartera/cupos", "Lending.CreditLimits.Read"),
        // Fase 7, US3 (T0462): el desembolso con sus órdenes y archivos, y los dobles de prueba (el menú los dibuja sólo si el servidor los
        // publica). Las garantías y el pagaré viven en el detalle de la solicitud (GarantiasYPagare).
        ("Pages/Cartera/Desembolsos.razor", "/cartera/desembolsos", "Lending.Disbursements.Execute"),
        ("Pages/Cartera/DoblesDePrueba.razor", "/cartera/dobles-de-prueba", "Lending.TestDoubles.Operate"),
        // Fase 8, US4 (T0510): los créditos; su detalle (/cartera/creditos/{id:guid}, T0509) se abre desde la lista.
        ("Pages/Cartera/Creditos.razor", "/cartera/creditos", "Lending.Loans.Read"),
        // Fase 10, US6 (T0602): la bandeja de mensajes —los recibos y las corridas del proceso—.
        ("Pages/Cartera/BandejaDeMensajes.razor", "/cartera/bandeja-de-mensajes", "Lending.Messages.Read"),
        // Fase 11, US7 (T0611; DT §2.16): la conciliación contable de Cartera y, en el grupo de Contabilidad, la matriz, los tipos de
        // comprobante, la completitud, los lotes y los indicadores regulatorios.
        ("Pages/Cartera/ConciliacionContable.razor", "/cartera/conciliacion-contable", "Lending.Reconciliation.View"),
        ("Pages/Contabilidad/Cartera/Matriz.razor", "/contabilidad/cartera/matriz", "Accounting.LendingRules.View"),
        ("Pages/Contabilidad/Cartera/TiposDeComprobanteDeCartera.razor", "/contabilidad/cartera/tipos-de-comprobante", "Accounting.LendingRules.View"),
        ("Pages/Contabilidad/Cartera/Completitud.razor", "/contabilidad/cartera/completitud", "Accounting.LendingRules.View"),
        ("Pages/Contabilidad/Cartera/Lotes.razor", "/contabilidad/cartera/lotes", "Accounting.LendingBatches.View"),
        ("Pages/Contabilidad/IndicadoresRegulatorios.razor", "/contabilidad/indicadores-regulatorios", "Accounting.RegulatoryIndicators.View"),
        // Fase 12, US9 (T0714, T0715): los cierres de Cartera con la calificación informativa (a ella remite Accounting.Period.LendingNotClosed).
        ("Pages/Cartera/Cierres.razor", "/cartera/cierres", "Lending.Ratings.Read"),
        // Fase 13, US8 (T0759–T0761): la puesta en marcha —la carga de migración y la activación—.
        ("Pages/Cartera/Migracion.razor", "/cartera/migracion", "Lending.Migration.Load"),
        ("Pages/Cartera/Activacion.razor", "/cartera/activacion", "Lending.Activation.Execute"),
        // Fase 14, US13 (T0792, T0793): los documentos al deudor y la verificación de su código (el QR la abre con sesión iniciada).
        ("Pages/Cartera/Documentos.razor", "/cartera/documentos", "Lending.Documents.Read"),
        ("Pages/Cartera/Verificar.razor", "/cartera/verificar", "Lending.Documents.Verify"),
        // Fase 14, US15 (T0800): el centro de informes de Cartera (las siete vistas de K1).
        ("Pages/Cartera/Informes.razor", "/cartera/informes", "Lending.Reports.View"),
        // Fase 15, US10 (T0852, T0853; K2): la libranza —pagadurías, archivos de descuento y no aplicados—.
        ("Pages/Cartera/Pagadurias.razor", "/cartera/pagadurias", "Lending.PayrollAgreements.View"),
        ("Pages/Cartera/ArchivosDeDescuento.razor", "/cartera/archivos-de-descuento", "Lending.PayrollDeductionFiles.Read"),
        ("Pages/Cartera/NoAplicados.razor", "/cartera/no-aplicados", "Lending.PayrollDeductionFiles.Read"),
    ];

    /// <summary>Las pantallas de detalle, sin enlace en el menú: existen, protegidas y con indicador.</summary>
    private static readonly (string Archivo, string Ruta, string Permiso)[] Detalles =
    [
        ("Pages/Cartera/LineaDetalle.razor", "/cartera/lineas/{Id:guid}", "Lending.CreditLines.View"),
        // US2 (T0414): la solicitud nueva y su detalle se abren desde Solicitudes.
        ("Pages/Cartera/SolicitudNueva.razor", "/cartera/solicitudes/nueva", "Lending.Applications.Create"),
        ("Pages/Cartera/SolicitudDetalle.razor", "/cartera/solicitudes/{Id:guid}", "Lending.Applications.Read"),
        // US4 (T0509): el expediente del crédito; la pestaña de liquidación exige además Lending.Payoffs.Read (se comprueba aparte).
        ("Pages/Cartera/CreditoDetalle.razor", "/cartera/creditos/{Id:guid}", "Lending.Loans.Read"),
        // US7 (T0611): el detalle de un lote de Cartera se abre desde Lotes y desde el origen de un comprobante.
        ("Pages/Contabilidad/Cartera/Lote.razor", "/contabilidad/cartera/lotes/{Id:guid}", "Accounting.LendingBatches.View"),
        // US9 (T0714): el detalle de un cierre se abre desde Cierres y desde la calificación del crédito.
        ("Pages/Cartera/Cierre.razor", "/cartera/cierres/{Id:guid}", "Lending.Ratings.Read"),
        // US10 (T0852): el detalle de una pagaduría y el de un archivo de descuentos se abren desde sus listas.
        ("Pages/Cartera/Pagaduria.razor", "/cartera/pagadurias/{Id:guid}", "Lending.PayrollAgreements.View"),
        ("Pages/Cartera/ArchivoDeDescuento.razor", "/cartera/archivos-de-descuento/{Id:guid}", "Lending.PayrollDeductionFiles.Read"),
    ];

    /// <summary>
    /// US9 (T0715): la política SARC vive en la pestaña «Riesgo» de Parámetros y la calificación en la del crédito; los dos componentes cargan con
    /// su indicador y escriben detrás de su permiso. Contabilidad remite a Cierres cuando Cartera no cerró el mes.
    /// </summary>
    [Fact]
    public void La_politica_y_la_calificacion_del_credito_estan_en_sus_pantallas()
    {
        var politica = File.ReadAllText(Path.Combine(Shared, "Components", "Cartera", "PoliticaSarc.razor"));
        var calificacion = File.ReadAllText(Path.Combine(Shared, "Components", "Cartera", "CalificacionDelCredito.razor"));
        Assert.Contains("<IndicadorDeCarga", politica, StringComparison.Ordinal);
        Assert.Contains("Required=\"Lending.RiskPolicy.Manage\"", politica, StringComparison.Ordinal);
        Assert.Contains("Required=\"Lending.RiskPolicy.Validate\"", politica, StringComparison.Ordinal);
        Assert.Contains("<IndicadorDeCarga", calificacion, StringComparison.Ordinal);
        Assert.Contains("Required=\"Lending.Ratings.Override\"", calificacion, StringComparison.Ordinal);
        Assert.Contains("<PoliticaSarc", File.ReadAllText(Path.Combine(Shared, "Pages", "Cartera", "Parametros.razor")), StringComparison.Ordinal);
        Assert.Contains("<CalificacionDelCredito", File.ReadAllText(Path.Combine(Shared, "Pages", "Cartera", "CreditoDetalle.razor")), StringComparison.Ordinal);
        Assert.Contains("href=\"/cartera/cierres\"", File.ReadAllText(Path.Combine(Shared, "Pages", "Contabilidad", "Periodos.razor")), StringComparison.Ordinal);
    }

    [Fact]
    public void Cada_detalle_existe_protegido_y_con_indicador()
    {
        var infracciones = new List<string>();
        foreach (var (archivo, ruta, permiso) in Detalles)
        {
            var camino = Path.Combine(Shared, archivo.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(camino)) { infracciones.Add($"{archivo}: no existe"); continue; }
            var texto = File.ReadAllText(camino);
            if (!texto.Contains($"@page \"{ruta}\"", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin @page {ruta}");
            if (!texto.Contains($"Required=\"{permiso}\"", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin permiso {permiso}");
            if (!texto.Contains("<IndicadorDeCarga", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin IndicadorDeCarga");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Detalles de Cartera fuera del contrato (T98)");
    }

    /// <summary>
    /// US4 (T0509): la liquidación es una lectura auditada con su propio permiso: la pestaña va detrás de <c>Lending.Payoffs.Read</c> y cada
    /// zona que carga (resumen, liquidación, estado de cuenta, plan, operaciones, comprobante, libro, garantías) tiene su indicador.
    /// </summary>
    [Fact]
    public void El_detalle_del_credito_protege_la_liquidacion_y_carga_por_zona()
    {
        var texto = File.ReadAllText(Path.Combine(Shared, "Pages", "Cartera", "CreditoDetalle.razor"));
        Assert.Contains("Required=\"Lending.Payoffs.Read\"", texto, StringComparison.Ordinal);
        Assert.True(Regex.Matches(texto, "<IndicadorDeCarga ").Count >= 8, "CreditoDetalle: un IndicadorDeCarga por zona que carga.");
    }

    [Fact]
    public void Cada_pantalla_existe_protegida_con_indicador_y_en_el_menu()
    {
        var menu = File.ReadAllText(Path.Combine(Shared, "Layout", "NavMenu.razor"));
        var infracciones = new List<string>();
        foreach (var (archivo, ruta, permiso) in Pantallas)
        {
            var camino = Path.Combine(Shared, archivo.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(camino)) { infracciones.Add($"{archivo}: no existe"); continue; }
            var texto = File.ReadAllText(camino);
            if (!texto.Contains($"@page \"{ruta}\"", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin @page {ruta}");
            if (!texto.Contains($"Required=\"{permiso}\"", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin permiso {permiso}");
            if (!texto.Contains("<IndicadorDeCarga", StringComparison.Ordinal)) infracciones.Add($"{archivo}: sin IndicadorDeCarga");
            if (!menu.Contains($"href=\"{ruta}\"", StringComparison.Ordinal)) infracciones.Add($"{ruta}: el menú no la enlaza");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Pantallas de Cartera fuera del contrato (T98)");
    }
}
