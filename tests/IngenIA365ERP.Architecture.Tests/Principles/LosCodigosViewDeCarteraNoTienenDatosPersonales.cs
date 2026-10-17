using System.Reflection;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Parameters.ListParameters;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Identity.Seed;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T60, C84): un código <c>Lending.*.View</c> no da acceso a datos personales; la bandeja,
/// los hechos y los archivos regulatorios van con <c>Read</c>.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0006) y la llena cada historia con sus rutas que devuelven datos de deudores y el permiso que exigen; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
///
/// <para>
/// US17 (T0261, T0273) la llena con tres reglas: (1) los <c>Lending.*.View</c> sembrados son exactamente los once de api.md §2.1 —los roles
/// integrados Operator, ReadOnly y Auditor los reciben todos por su glob—; (2) las rutas de esta fase que devuelven personas con documento o
/// datos del deudor se abren con un código que no es <c>View</c>, y su archivo de rutas lo exige de verdad; (3) los DTO que devuelven las
/// rutas <c>.View</c> de esta fase no tienen propiedades de documento, correo, teléfono ni dirección (de los miembros de un órgano, sólo
/// nombre y cargo, C84).
/// </para>
/// </summary>
public class LosCodigosViewDeCarteraNoTienenDatosPersonales
{
    /// <summary>
    /// Los recursos con <c>View</c> de las tablas de api.md §2.1: ninguno devuelve datos del deudor. Son trece: la lista de T0261 nombra once
    /// y omite <c>Reconciliation.View</c> (conciliación contable y cuadre, cifras) y <c>Reports.View</c> (centro de informes, cuyas vistas con
    /// datos del deudor exigen además <c>Reports.ExportPersonalData</c> al exportar, §1.10), que las tablas sí tienen y el seeder siembra.
    /// </summary>
    private static readonly string[] RecursosConView =
    [
        "CreditLines", "Parameters", "Scoring", "Attributions", "GoverningBodies", "MessageTemplates", "Processes", "PayrollAgreements",
        "Dashboard", "Templates", "Alerts", "Reconciliation", "Reports",
    ];

    /// <summary>Rutas que devuelven datos personales, el permiso que exigen y el archivo de rutas que lo declara.</summary>
    private static readonly (string Ruta, string Permiso, string Archivo)[] RutasConDatosPersonales =
    [
        // US17 (T0261): vinculados con su documento, actas con los nombres de los votantes y los excluidos, el alcance de un usuario,
        // los reclamos del titular.
        ("GET /api/lending/related-parties", "Lending.RelatedParties.Read", "RelatedPartiesEndpoints.cs"),
        ("GET /api/lending/sessions/{id}", "Lending.Sessions.Read", "SessionsEndpoints.cs"),
        ("GET /api/lending/sessions/{id}/minutes.pdf", "Lending.Sessions.Read", "SessionsEndpoints.cs"),
        ("GET /api/lending/scopes/users/{userPublicId}", "Lending.Scopes.Manage", "ScopesEndpoints.cs"),
        ("GET /api/lending/credit-report-claims", "Lending.CreditReportClaims.Read", "CreditReportClaimsEndpoints.cs"),
        // US17 (T0273): la cola de mensajes al deudor (destinatario, cuerpo, destino copiado).
        ("GET /api/lending/debtor-messages", "Lending.DebtorMessages.Read", "DebtorMessagesEndpoints.cs"),
        ("GET /api/lending/debtor-messages/{id}", "Lending.DebtorMessages.Read", "DebtorMessagesEndpoints.cs"),
        // US7 (T0610; api.md §16.1, §16.3, DT §2.14): los hechos contables y la lista por crédito de una parte (número, tercero, valores) y la
        // bandeja de mensajes emitidos, con Messages.Read y nunca con un .View. La conciliación (Reconciliation.View) son cifras por conjunto.
        ("GET /api/lending/accounting/facts", "Lending.Messages.Read", "AccountingIntegrationEndpoints.cs"),
        ("GET /api/lending/accounting/fact-parts/{id}", "Lending.Messages.Read", "AccountingIntegrationEndpoints.cs"),
        ("GET /api/lending/messages", "Lending.Messages.Read", "LendingMessagesEndpoints.cs"),
        ("GET /api/lending/messages/{id}", "Lending.Messages.Read", "LendingMessagesEndpoints.cs"),
        // US9 (bloque 12C, T0712; api.md §12.2, §12.3): la calificación informativa, el análisis, las líneas y el reporte de cambios del cierre y la
        // calificación de un crédito nombran al deudor: van con Ratings.Read. La política (Parameters.View) y el cuadre del cierre
        // (Reconciliation.View) no traen deudores (sus DTO están abajo).
        ("GET /api/lending/risk/ratings", "Lending.Ratings.Read", "RiskEndpoints.cs"),
        ("GET /api/lending/risk/analysis-ratings", "Lending.Ratings.Read", "RiskEndpoints.cs"),
        ("GET /api/lending/risk/monthly-closes/{id}/loans", "Lending.Ratings.Read", "RiskEndpoints.cs"),
        ("GET /api/lending/risk/monthly-closes/{id}/changes", "Lending.Ratings.Read", "RiskEndpoints.cs"),
        ("GET /api/lending/loans/{id}/rating", "Lending.Ratings.Read", "RiskEndpoints.cs"),
        // K5, US16 (fase 21, T0994): los archivos regulatorios traen deudores (documento, saldos, mora, motivo de exclusión): listar, leer y descargar
        // van con RegulatoryFiles.Read, nunca con un View (DT §2.25).
        ("GET /api/lending/regulatory/files", "Lending.RegulatoryFiles.Read", "RegulatoryEndpoints.cs"),
        ("GET /api/lending/regulatory/files/{id}", "Lending.RegulatoryFiles.Read", "RegulatoryEndpoints.cs"),
        ("POST /api/lending/regulatory/files/{id}/download-link", "Lending.RegulatoryFiles.Read", "RegulatoryEndpoints.cs"),
    ];

    /// <summary>Los DTO que devuelven las rutas <c>.View</c> de esta fase (se recorren con sus tipos anidados).</summary>
    private static readonly Type[] DtosDeView =
    [
        typeof(AttributionMatrixDto), typeof(AttributionRoutingPreviewDto), typeof(PermissionAmountLimitDto),
        typeof(GoverningBodyListItemDto), typeof(GoverningBodyDto),
        typeof(ParameterDto), typeof(LendingTemplateDto),
        typeof(AlertDto), typeof(AlertTypeDto),
        // T0273: las plantillas de mensaje y su vista previa (MessageTemplates.View).
        typeof(MessageTemplateVersionDto), typeof(MessageTemplatePreviewDto),
        // T0610: la conciliación contable (Reconciliation.View): conjuntos de cuentas, cifras y hechos sin deudor.
        typeof(IngenIA365ERP.Application.Lending.Integration.ConciliacionContableDto),
        // US9 (bloque 12C): la política SARC efectiva (Parameters.View), con la norma, las celdas y las versiones.
        typeof(IngenIA365ERP.Application.Lending.Risk.RiskPolicyDto),
        // US8 (bloque 13C, T0728): el cuadre de la carga de migración (Reconciliation.View): por concepto, modalidad, sub-segmento, categoría y
        // conjunto de cuentas, sin documentos ni nombres.
        typeof(IngenIA365ERP.Application.Lending.Migration.MigrationBalanceCheckDto),
    ];

    /// <summary>
    /// Nombres que el patrón atrapa sin ser datos de una persona: <c>RiskPolicyNormDto.DocumentPublicId</c> es el documento normativo (el Anexo 1
    /// registrado en la central), no el documento de identidad de nadie (US9, bloque 12C).
    /// </summary>
    private static readonly HashSet<string> NoSonPersonales = new(StringComparer.Ordinal) { "RiskPolicyNormDto.DocumentPublicId" };

    private static readonly Regex PropiedadPersonal = new(
        @"(Document|TaxId|Identification|Email|Mail|Phone|Mobile|Celular|Telefono|Address|Direccion|BirthDate)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void Ninguna_ruta_con_datos_personales_se_abre_con_View()
    {
        var api = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API", "Endpoints", "Lending");
        var infracciones = new List<string>();
        foreach (var (ruta, permiso, archivo) in RutasConDatosPersonales)
        {
            if (permiso.EndsWith(".View", StringComparison.Ordinal)) infracciones.Add($"{ruta}: {permiso}");
            var camino = Path.Combine(api, archivo);
            if (!File.Exists(camino)) { infracciones.Add($"{ruta}: no existe {archivo}"); continue; }
            if (!File.ReadAllText(camino).Contains($"\"{permiso}\"", StringComparison.Ordinal))
                infracciones.Add($"{ruta}: {archivo} no declara {permiso}");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Una ruta con datos personales se abre con un código View (T60)");
    }

    [Fact]
    public void Los_View_de_Cartera_sembrados_son_los_del_contrato()
    {
        var campo = typeof(LendingPermissionCatalogSeeder).GetField("Catalog", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("LendingPermissionCatalogSeeder.Catalog no existe: actualizá la prueba.");
        var catalogo = ((string Resource, string Action, string Description)[])campo.GetValue(null)!;

        var view = catalogo.Where(p => p.Action == "View").Select(p => p.Resource["Lending.".Length..]).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(RecursosConView.Order(StringComparer.Ordinal), view);
    }

    [Fact]
    public void Los_DTO_de_las_rutas_View_no_tienen_datos_personales()
    {
        var infracciones = new List<string>();
        var vistos = new HashSet<Type>();
        foreach (var dto in DtosDeView) Revisar(dto, dto.Name, vistos, infracciones);
        ReglaSobreFuente.SinInfracciones(infracciones, "Un DTO de una ruta View trae datos personales (C84)");
    }

    /// <summary>
    /// US15 (T0773, C84; api.md §17): el centro de informes se abre con <c>Lending.Reports.View</c>, así que toda vista que trae datos del
    /// deudor (nombre, documento, crédito) declara <c>PersonalData</c> y exige además <c>Lending.Loans.Read</c> —que no es un <c>View</c>— para
    /// consultarla; sin la marca, las columnas visibles no nombran a nadie (las agregadas: colocación y recaudo).
    /// </summary>
    [Fact]
    public void Las_vistas_de_informes_con_datos_del_deudor_exigen_Loans_Read()
    {
        var vistas = typeof(IngenIA365ERP.Application.Lending.Reports.VistaDeInformeDeCartera).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("IngenIA365ERP.Application.Lending.Reports", StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.FieldType == typeof(IngenIA365ERP.Application.Lending.Reports.VistaDeInformeDeCartera))
            .Select(f => (Tipo: f.DeclaringType!, Vista: (IngenIA365ERP.Application.Lending.Reports.VistaDeInformeDeCartera)f.GetValue(null)!))
            .ToList();
        Assert.True(vistas.Count >= 7, $"Se esperaban al menos las siete vistas de K1 y hay {vistas.Count}.");

        var infracciones = new List<string>();
        foreach (var (tipo, vista) in vistas)
        {
            if (vista.PersonalData && vista.RequiredPermission != "Lending.Loans.Read")
                infracciones.Add($"{vista.Key}: trae datos del deudor y no exige Lending.Loans.Read");
            if (vista.RequiredPermission?.EndsWith(".View", StringComparison.Ordinal) == true)
                infracciones.Add($"{vista.Key}: su permiso adicional es un View");
            if (vista.PersonalData) continue;
            var columnas = tipo.GetField("Columnas", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                as IReadOnlyList<IngenIA365ERP.Application.Common.Reports.ColumnaExportable> ?? [];
            foreach (var c in columnas.Where(c => !c.EsOculta && Regex.IsMatch(c.Nombre, "^(Deudor|Documento|Cliente|Destinatario|Crédito|Titular)$", RegexOptions.IgnoreCase)))
                infracciones.Add($"{vista.Key}: sin PersonalData muestra la columna «{c.Nombre}»");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Una vista del centro de informes de Cartera trae datos del deudor sin pedirlos (C84)");
    }

    /// <summary>
    /// US15 (T0773, DT §2.25): el DTO del tablero (<c>LendingDashboardDto</c>, ruta <c>Lending.Dashboard.View</c>) no expone persona, documento ni
    /// crédito —sólo fichas con cifras y su enlace—. El tablero es de K5: mientras el tipo no exista la regla queda preparada y no falla; en cuanto
    /// aparezca, se revisa con sus tipos anidados.
    /// </summary>
    [Fact]
    public void El_DTO_del_tablero_no_expone_persona_documento_ni_credito()
    {
        var tablero = typeof(IngenIA365ERP.Application.Lending.Reports.VistaDeInformeDeCartera).Assembly.GetTypes()
            .FirstOrDefault(t => t.Name == "LendingDashboardDto");
        if (tablero is null) return;

        var infracciones = new List<string>();
        Revisar(tablero, tablero.Name, [], infracciones);
        RevisarIdentificadores(tablero, tablero.Name, [], infracciones);
        ReglaSobreFuente.SinInfracciones(infracciones, "El DTO del tablero de Cartera expone datos del deudor (DT §2.25)");
    }

    private static readonly Regex IdentificadorDeDeudor = new(@"(Person|Persona|Debtor|Deudor|Loan|Credito|Crédito|Holder|Titular)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static void RevisarIdentificadores(Type tipo, string camino, HashSet<Type> vistos, List<string> infracciones)
    {
        tipo = Nullable.GetUnderlyingType(tipo) ?? tipo;
        if (tipo.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(tipo))
        {
            foreach (var a in tipo.GetGenericArguments()) RevisarIdentificadores(a, camino, vistos, infracciones);
            return;
        }
        if (tipo.IsArray) { RevisarIdentificadores(tipo.GetElementType()!, camino, vistos, infracciones); return; }
        if (tipo.Namespace?.StartsWith("IngenIA365ERP", StringComparison.Ordinal) != true || tipo.IsEnum || !vistos.Add(tipo)) return;
        foreach (var p in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (IdentificadorDeDeudor.IsMatch(p.Name)) infracciones.Add($"{camino}.{p.Name}");
            RevisarIdentificadores(p.PropertyType, $"{camino}.{p.Name}", vistos, infracciones);
        }
    }

    private static void Revisar(Type tipo, string camino, HashSet<Type> vistos, List<string> infracciones)
    {
        tipo = Nullable.GetUnderlyingType(tipo) ?? tipo;
        if (tipo.IsGenericType && tipo.GetGenericArguments() is { Length: > 0 } argumentos && typeof(System.Collections.IEnumerable).IsAssignableFrom(tipo))
        {
            foreach (var a in argumentos) Revisar(a, camino, vistos, infracciones);
            return;
        }
        if (tipo.IsArray) { Revisar(tipo.GetElementType()!, camino, vistos, infracciones); return; }
        if (tipo.Namespace?.StartsWith("IngenIA365ERP", StringComparison.Ordinal) != true || tipo.IsEnum || !vistos.Add(tipo)) return;

        foreach (var p in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (PropiedadPersonal.IsMatch(p.Name) && !NoSonPersonales.Contains($"{tipo.Name}.{p.Name}")) infracciones.Add($"{camino}.{p.Name}");
            Revisar(p.PropertyType, $"{camino}.{p.Name}", vistos, infracciones);
        }
    }
}
