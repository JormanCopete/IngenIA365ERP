namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos de la migración y la activación de Cartera (feature 013, US8, T0759, T0760): las 17 partes en su orden de carga (plantillas.md §5.2),
/// los estados de la carga y de las tandas, las clases de diferencia y de venta, y los requisitos y avisos de la activación (api.md §19.3). Ninguno
/// nombra al sistema anterior. (nuevo)
/// </summary>
public static class TextosDeMigracion
{
    /// <summary>Las partes en su orden de carga: clave en la ruta, nombre y si es obligatoria.</summary>
    public static readonly IReadOnlyList<(string Clave, string Nombre, bool Obligatoria)> Partes =
    [
        ("loans", "Créditos", true),
        ("installments", "Cuotas pendientes", true),
        ("balances", "Saldos por concepto", true),
        ("risk-data", "Mora, calificación y deterioro", true),
        ("restructurings", "Reestructuraciones", false),
        ("co-debtors", "Codeudores y avalistas", false),
        ("collaterals", "Garantías y pagarés", false),
        ("charged-off", "Cartera castigada", false),
        ("sales", "Ventas a crédito por factura", false),
        ("credit-limits", "Cupos", false),
        ("blocks", "Bloqueos, embargos, fallecimientos e insolvencias", false),
        ("authorizations", "Autorizaciones del titular", false),
        ("prior-notices", "Avisos previos enviados", false),
        ("bureau-reports", "Reportes vigentes en centrales", false),
        ("claims", "Reclamos en trámite", false),
        ("related-parties", "Personas vinculadas", false),
        ("arrears-history", "Historial de mora mensual", false),
    ];

    public static string Parte(string clave) => Partes.FirstOrDefault(p => p.Clave == clave).Nombre ?? clave;

    public static string EstadoDeCarga(int estado) => estado switch
    {
        1 => "Borrador",
        2 => "Aplicada",
        3 => "Reemplazada",
        4 => "Activada",
        _ => estado.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string EstadoDeTanda(int estado) => estado == 2 ? "Reemplazada" : "Vigente";

    public static string ClaseDeDiferencia(int clase) => clase switch
    {
        1 => "Cuadre contable",
        2 => "Venta no migrada",
        3 => "Venta fuera de Cartera",
        _ => clase.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>Las clases de una venta de la conciliación (1 a 4).</summary>
    public static readonly IReadOnlyList<(int Valor, string Texto)> ClasesDeVenta =
    [
        (1, "Migrada"),
        (2, "Cancelada antes del corte"),
        (3, "No migrada con saldo"),
        (4, "Fuera de Cartera (servicios a asociados)"),
    ];

    public static string ClaseDeVenta(int clase) => ClasesDeVenta.FirstOrDefault(c => c.Valor == clase).Texto ?? clase.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string EstadoDeRequisito(string estado) => estado switch
    {
        "Met" => "Cumplido",
        "Missing" => "Falta",
        "NotApplicable" => "No aplica",
        // Sólo en activaciones anteriores al 2026-10-18: desde entonces todo requisito se exige en todos los ambientes.
        "Waived" => "Falta (fuera de producción no bloqueaba)",
        _ => estado,
    };

    public static string Requisito(string codigo) => codigo switch
    {
        "LoadApplied" => "Carga de migración aplicada",
        "DifferencesAccepted" => "Diferencias del cuadre aceptadas",
        "SalesReconciled" => "Ventas a crédito conciliadas",
        "DateInTerm" => "Fecha dentro del plazo",
        "AccountingMonthOpen" => "Contabilidad iniciada",
        "ReceiptsCollectorRegistered" => "Recaudo registrado",
        "TreasuryDestinationRegistered" => "Tesorería registrada",
        "ExpectedLossAvailable" => "Modelo de pérdida esperada disponible",
        "ExpectedLossParameters" => "Parámetros de pérdida esperada",
        "ArrearsHistoryComplete" => "Historial de mora completo",
        "Annex1Current" => "Anexo 1 vigente",
        "UsuryForActiveLines" => "Tasa de usura de cada línea activa",
        // Decisión del dueño del 2026-10-18 (feature 014, D18): los perfiles van por módulo.
        "LendingRolesCreated" => "Roles de crédito de Cartera creados",
        "DepositRolesCreated" => "Roles de captaciones y aportes creados",
        "LoansActivated" => "Créditos activados primero",
        "AccountsCollectorRegistered" => "Recaudador de cuentas registrado",
        "PayrollConceptsReady" => "Conceptos de nómina de aportes y ahorros",
        "DeliveriesComplete" => "Entregas K7 a K11 desplegadas",
        "ProfileAllowsClasses" => "Perfil regulatorio que autoriza captar",
        "LinesWithMatrix" => "Matriz contable de cada línea activa",
        "OfficesLinkedToAccountingBranches" => "Oficinas vinculadas a sucursales contables",
        "PayrollBridgeRuleResolved" => "Regla del recaudo por nómina",
        "CommercialParametersToAdjust" => "Lo que Comercial ajusta el mismo día",
        "PayrollConceptAccounts" => "Cuentas del descuento de Cartera en Nómina",
        "UsersWithoutScope" => "Usuarios sin alcance de oficinas",
        "AccountingMonthClosed" => "Mes de la activación cerrado en Contabilidad",
        _ => codigo,
    };
}
