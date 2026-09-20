using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Accounting.Budgets;

// Presupuesto del ejercicio (feature 009 E2, US9, FR-061..FR-064; contracts/api.md §10). Los
// contratos de entrada y salida de /api/accounting/budgets: la pantalla manda una fila por
// (cuenta, sucursal, centro) con sus doce valores y recibe el presupuesto con sus versiones.

/// <summary>Una fila del presupuesto tal como la digita la pantalla: la cuenta y sus doce valores (enero..diciembre), en pesos.</summary>
public sealed record BudgetLineInput(Guid AccountPublicId, Guid? BranchPublicId, Guid? CostCenterPublicId, IReadOnlyList<decimal> Amounts);

/// <summary>Una versión del presupuesto: la 1 es la inicial; modificar una aprobada crea la siguiente y deja ésta <c>Superseded</c>.</summary>
public sealed record BudgetVersionDto(int Version, string Status, DateTime? ApprovedAt, string? ApprovedBy, string? ChangeReason, DateTime CreatedAt, string? CreatedBy);

public sealed record BudgetLineDto(Guid AccountPublicId, string AccountCode, string AccountName, Guid? BranchPublicId, string? BranchName,
    Guid? CostCenterPublicId, string? CostCenterName, IReadOnlyList<decimal> Amounts, decimal Total);

/// <summary>
/// El presupuesto de un año en la versión pedida (o la vigente). <see cref="Status"/> viaja como
/// texto —«None» cuando el año no tiene presupuesto: es un éxito con la lista vacía, no un 404,
/// para que la pantalla abra el año en blanco y deje digitar— y no como número, para que la
/// pantalla no dependa del convertidor global de enums.
/// </summary>
public sealed record BudgetDto(int Year, int Version, string Status /* None|Draft|Approved|Superseded */, DateTime? ApprovedAt, string? ApprovedBy,
    string? ChangeReason, IReadOnlyList<BudgetVersionDto> Versions, IReadOnlyList<BudgetLineDto> Lines)
{
    public const string SinPresupuesto = "None";

    public static BudgetDto Ninguno(int year) => new(year, 0, SinPresupuesto, null, null, null, [], []);
}

/// <summary>Códigos de error del presupuesto (contracts/api.md §10). Mensajes en español y accionables (Principio VIII).</summary>
public static class BudgetErrors
{
    public static readonly Error FiscalYearNotFound = new("Accounting.Budget.FiscalYearNotFound",
        "Ese ejercicio no existe: ábralo en Contabilidad › Períodos antes de presupuestarlo.");
    public static readonly Error AlreadyExists = new("Accounting.Budget.AlreadyExists",
        "El año ya tiene presupuesto: modifíquelo en vez de crearlo otra vez.");
    public static readonly Error NotFound = new("Accounting.Budget.NotFound", "El año no tiene presupuesto todavía.");
    public static readonly Error ReasonRequired = new("Accounting.Budget.ReasonRequired",
        "El presupuesto vigente está aprobado: indique el motivo del cambio; quedará como una versión nueva.");
    public static readonly Error NotDraft = new("Accounting.Budget.NotDraft", "El presupuesto vigente no es un borrador.");
    public static readonly Error SourceNotFound = new("Accounting.Budget.SourceNotFound", "El año anterior no tiene presupuesto del que copiar.");
    public static readonly Error NegativeAmount = new("Accounting.Budget.NegativeAmount", "Los valores presupuestados no pueden ser negativos.");

    /// <summary>Sólo se presupuestan cuentas de movimiento activas (FR-061); <c>data.accountCode</c> dice cuál falló.</summary>
    public static Error AccountNotMovement(string accountCode) =>
        new ErrorConDatos("Accounting.Budget.AccountNotMovement",
            $"La cuenta {accountCode} no es de movimiento o está inactiva: presupueste sobre sus auxiliares.",
            new { accountCode });

    public static Error AccountNotFound(Guid accountPublicId) =>
        new("Accounting.Budget.AccountNotFound", $"La cuenta {accountPublicId} no existe en el plan.");
    public static readonly Error BranchNotFound = new("Accounting.Budget.BranchNotFound", "La sucursal indicada no existe.");
    public static readonly Error CostCenterNotFound = new("Accounting.Budget.CostCenterNotFound", "El centro de costo indicado no existe.");

    public static Error LineDuplicate(string accountCode) =>
        new ErrorConDatos("Accounting.Budget.LineDuplicate",
            $"La cuenta {accountCode} viene dos veces con la misma sucursal y centro de costo: deje una sola fila.",
            new { accountCode });

    public static Error InvalidDistribution(string detalle) =>
        new("Accounting.Budget.InvalidDistribution", $"La distribución no es válida: {detalle}");

    public static Error TwelveAmountsRequired(string accountCode) =>
        new ErrorConDatos("Accounting.Budget.InvalidDistribution",
            $"La cuenta {accountCode} necesita exactamente doce valores (enero a diciembre).", new { accountCode });
}
