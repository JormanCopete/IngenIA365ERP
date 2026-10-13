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
    ];

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
