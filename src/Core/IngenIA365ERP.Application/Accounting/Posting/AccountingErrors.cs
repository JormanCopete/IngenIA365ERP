using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Accounting.Posting;

/// <summary>Gravedad de una infracción de línea: los avisos se muestran y no impiden contabilizar (U3).</summary>
public enum Severidad { Error = 1, Aviso = 2 }

/// <summary>
/// Una infracción de una línea del comprobante, con el campo al que pertenece para que la pantalla
/// la señale. <c>LineNumber</c> 0 es una infracción del encabezado (fecha, tipo, cuadre).
/// </summary>
public sealed record ErrorDeLinea(int LineNumber, string Field, string Code, string Message, Severidad Severidad = Severidad.Error, string? AccountCode = null)
{
    public bool Bloquea => Severidad == Severidad.Error;
    public bool EsDeEncabezado => LineNumber <= 0;

    /// <summary>Como <see cref="Error"/> de un <see cref="Result"/>: los de línea llevan <c>data</c> (FR-041); los de encabezado van tal cual.</summary>
    public Error ComoError() => EsDeEncabezado
        ? new Error(Code, Message)
        : new ErrorConDatos(Code, $"Línea {LineNumber}: {Message}",
            new { lineNumber = LineNumber, field = Field, accountCode = AccountCode, rule = Code, severity = Severidad.ToString() });

    public static ErrorDeLinea DeEncabezado(Error error, string field) => new(0, field, error.Code, error.Message);
}

/// <summary>
/// Códigos de error del módulo contable (feature 009, contracts/contabilizacion.md §2 y
/// contracts/api.md). Mensajes en español y accionables (Principio VIII); los de línea llevan
/// <c>lineNumber</c>, <c>accountCode</c> y <c>rule</c> en <c>data</c> (FR-041).
/// </summary>
public static class AccountingErrors
{
    // ---- estado general ----
    public static readonly Error NotInitialized = new("Accounting.NotInitialized",
        "La contabilidad no está iniciada. La inicia quien tenga el permiso Accounting.Setup.Manage (Contabilidad › Configuración inicial).");

    // ---- tipos de comprobante ----
    public static Error VoucherTypeNotFound(string code) =>
        new("Accounting.VoucherType.NotFound", $"No existe el tipo de comprobante {code}.");
    public static Error VoucherTypeInactive(string code) =>
        new("Accounting.VoucherType.Inactive", $"El tipo de comprobante {code} está inactivo.");
    public static Error VoucherTypeNotAllowedForModule(string code, string module) =>
        new("Accounting.VoucherType.NotAllowedForModule", $"El tipo de comprobante {code} no puede usarlo {NombreDeModulo(module)}.");
    public static Error VoucherTypeSeeded(string code) =>
        new("Accounting.VoucherType.Seeded", $"El tipo {code} viene sembrado: no se elimina ni cambia de uso.");
    public static readonly Error CrossDocumentTypeNotFound = new("Accounting.CrossDocumentType.NotFound", "No existe ese tipo de documento cruce.");
    public static Error VoucherTypeCodeDuplicate(string code) =>
        new("Accounting.VoucherType.CodeDuplicate", $"Ya existe el tipo de comprobante {code}.");

    // ---- períodos ----
    public static Error PeriodNotFound(DateOnly date) =>
        new("Accounting.Period.NotFound", $"No existe un período contable para {date:yyyy-MM}. Abra el ejercicio en Contabilidad › Períodos.");
    public static Error PeriodClosed(DateOnly date) =>
        new("Accounting.Period.Closed", $"El período contable {date:yyyy-MM} está cerrado: no se puede contabilizar con esa fecha.");
    public static Error DateInFuture(DateOnly date) =>
        new("Accounting.Date.InFuture", $"La fecha {date:yyyy-MM-dd} es posterior a hoy.");
    public static readonly Error PeriodHasDrafts = new("Accounting.Period.HasDrafts",
        "El período tiene comprobantes en borrador: contabilícelos o descártelos antes de cerrar.");
    public static readonly Error PeriodAlreadyClosed = new("Accounting.Period.AlreadyClosed", "El período ya está cerrado.");
    public static readonly Error PeriodNotClosed = new("Accounting.Period.NotClosed", "El período no está cerrado.");
    public static readonly Error ReasonRequired = new("Accounting.ReasonRequired", "Indique el motivo.");
    public static readonly Error FiscalYearPeriodsOpen = new("Accounting.FiscalYear.PeriodsOpen",
        "Cierre los doce períodos antes de cerrar el ejercicio.");
    public static readonly Error FiscalYearPreviousOpen = new("Accounting.FiscalYear.PreviousOpen",
        "El ejercicio anterior sigue abierto: ciérrelo primero.");
    public static readonly Error FiscalYearResultAccountMissing = new("Accounting.FiscalYear.ResultAccountMissing",
        "Defina la cuenta de resultado del ejercicio en Contabilidad › Configuración antes de cerrar el año.");
    public static readonly Error FiscalYearNotFound = new("Accounting.FiscalYear.NotFound", "No existe ese ejercicio.");
    public static readonly Error FiscalYearAlreadyExists = new("Accounting.FiscalYear.AlreadyExists", "Ese ejercicio ya existe.");
    public static readonly Error FiscalYearNotClosed = new("Accounting.FiscalYear.NotClosed", "El ejercicio no está cerrado.");
    public static readonly Error FiscalYearAlreadyClosed = new("Accounting.FiscalYear.AlreadyClosed", "El ejercicio ya está cerrado.");

    // ---- documento ----
    public static Error DocumentUnbalanced(decimal difference) =>
        new("Accounting.Document.Unbalanced", $"El comprobante no cuadra: la diferencia es {difference:N2}.");
    public static readonly Error DocumentTooFewLines = new("Accounting.Document.TooFewLines", "Un comprobante lleva al menos dos líneas.");
    public static readonly Error DocumentNotFound = new("Accounting.Document.NotFound", "No existe ese comprobante.");
    public static readonly Error DocumentNotDraft = new("Accounting.Document.NotDraft", "El comprobante ya no es un borrador.");
    public static readonly Error DocumentNotPosted = new("Accounting.Document.NotPosted", "Sólo se reversa un comprobante contabilizado.");
    public static readonly Error DocumentAlreadyReversed = new("Accounting.Document.AlreadyReversed", "El comprobante ya fue reversado.");
    public static readonly Error DocumentIsReversal = new("Accounting.Document.IsReversal", "Una reversión no se reversa: reverse el original.");
    public static Error DocumentModuleOwned(string module) =>
        new("Accounting.Document.ModuleOwned", $"Este comprobante lo generó {NombreDeModulo(module)}: sólo desde ahí se anula.");
    public static readonly Error DocumentFourEyes = new("Accounting.Document.FourEyes",
        "La cooperativa exige cuatro ojos: quien contabiliza debe ser distinto de quien registró el borrador.");
    public static readonly Error DocumentManualOnly = new("Accounting.Document.ManualOnly",
        "Los borradores sólo usan tipos de comprobante manuales.");

    /// <summary>Todas las infracciones bloqueantes juntas (FR-041): el mensaje resume las primeras y <c>data.errors</c> las trae completas.</summary>
    public static Error DocumentInvalid(IReadOnlyList<ErrorDeLinea> errores)
    {
        var bloqueantes = errores.Where(e => e.Bloquea).ToList();
        var detalle = string.Join(" · ", bloqueantes.Take(3).Select(e => e.EsDeEncabezado ? e.Message : $"línea {e.LineNumber}: {e.Message}"));
        var resto = bloqueantes.Count > 3 ? $" (y {bloqueantes.Count - 3} más)" : string.Empty;
        return new ErrorConDatos("Accounting.Document.Invalid",
            $"El comprobante tiene {bloqueantes.Count} error(es): {detalle}{resto}",
            new
            {
                errors = bloqueantes.Select(e => new
                {
                    lineNumber = e.LineNumber, field = e.Field, accountCode = e.AccountCode, rule = e.Code, message = e.Message, severity = e.Severidad.ToString(),
                }).ToList(),
            });
    }

    // ---- líneas (contrato §2, reglas 4 a 10) ----
    public static ErrorDeLinea LineAmountInvalid(int line) =>
        Linea(line, "Debit", "Accounting.Line.AmountInvalid", "Cada línea lleva débito o crédito mayor que cero, con dos decimales, nunca ambos.");
    public static ErrorDeLinea LineAccountNotFound(int line, string code) =>
        Linea(line, "Account", "Accounting.Line.AccountNotFound", $"La cuenta {code} no existe.", code);
    public static ErrorDeLinea LineAccountNotMovement(int line, string code) =>
        Linea(line, "Account", "Accounting.Line.AccountNotMovement", $"La cuenta {code} no es de movimiento; use una de sus auxiliares.", code);
    public static ErrorDeLinea LineAccountInactive(int line, string code) =>
        Linea(line, "Account", "Accounting.Line.AccountInactive", $"La cuenta {code} está inactiva.", code);
    public static ErrorDeLinea LineAccountNotEnabledForModule(int line, string code, string module) =>
        Linea(line, "Account", "Accounting.Line.AccountNotEnabledForModule", $"La cuenta {code} no está habilitada para {NombreDeModulo(module)}.", code);
    public static ErrorDeLinea LineBranchRequired(int line, string code) =>
        Linea(line, "Branch", "Accounting.Line.BranchRequired", $"La cuenta {code} exige elegir la sucursal.", code);
    public static ErrorDeLinea LineBranchInvalid(int line) =>
        Linea(line, "Branch", "Accounting.Line.BranchInvalid", "La sucursal no existe o está inactiva.");
    public static ErrorDeLinea LineBranchOutOfScope(int line) =>
        Linea(line, "Branch", "Accounting.Line.BranchOutOfScope", "Esa sucursal no está entre las asignadas a su usuario.");
    public static ErrorDeLinea LineThirdPartyRequired(int line, string code) =>
        Linea(line, "Person", "Accounting.Line.ThirdPartyRequired", $"La cuenta {code} exige tercero.", code);
    public static ErrorDeLinea LineThirdPartyInvalid(int line) =>
        Linea(line, "Person", "Accounting.Line.ThirdPartyInvalid", "El tercero no existe, está inactivo o fue eliminado.");
    public static ErrorDeLinea LineCrossDocumentRequired(int line, string code) =>
        Linea(line, "CrossDocument", "Accounting.Line.CrossDocumentRequired", $"La cuenta {code} exige documento cruce (tipo y número).", code);
    public static ErrorDeLinea LineCrossDocumentTypeInvalid(int line, string type) =>
        Linea(line, "CrossDocument", "Accounting.Line.CrossDocumentTypeInvalid", $"El tipo de documento cruce {type} no existe o está inactivo.");
    public static ErrorDeLinea LineCostCenterRequired(int line, string code) =>
        Linea(line, "CostCenter", "Accounting.Line.CostCenterRequired", $"La cuenta {code} exige centro de costo.", code);
    public static ErrorDeLinea LineCostCenterNotAllowed(int line, string code) =>
        Linea(line, "CostCenter", "Accounting.Line.CostCenterNotAllowed", $"La cuenta {code} no maneja centro de costo.", code);
    public static ErrorDeLinea LineCostCenterInvalid(int line) =>
        Linea(line, "CostCenter", "Accounting.Line.CostCenterInvalid", "El centro de costo no existe o está inactivo.");
    public static ErrorDeLinea LineTaxBaseRequired(int line, string code) =>
        Linea(line, "TaxBase", "Accounting.Line.TaxBaseRequired", $"La cuenta {code} es de impuesto: indique la base gravable.", code);
    public static ErrorDeLinea LineTaxAmountDiffers(int line, string code, decimal esperado) =>
        Linea(line, "TaxBase", "Accounting.Line.TaxAmountDiffers", $"Base × tarifa da {esperado:N2}; difiere del valor de la línea.", code, Severidad.Aviso);
    public static ErrorDeLinea LineTaxAmountMismatch(int line, string code, decimal esperado, decimal tolerancia) =>
        Linea(line, "TaxBase", "Accounting.Line.TaxAmountMismatch", $"Base × tarifa da {esperado:N2} y la diferencia supera la tolerancia de {tolerancia:N2}.", code);
    public static ErrorDeLinea LineTaxRateMissing(int line, string code) =>
        Linea(line, "TaxBase", "Accounting.Line.TaxRateMissing", $"La cuenta {code} no tiene tarifa vigente para la fecha.", code);

    // ---- parametrización y elegibilidad (otros módulos, FR-016) ----
    public static Error AccountNotEligible(string code, string module, string rule) =>
        new ErrorConDatos("Accounting.Account.NotEligible", $"La cuenta {code} no sirve para {NombreDeModulo(module)}: {rule}.",
            new { accountCode = code, module, rule });
    public static Error ParameterizationMissing(string module, string que) =>
        new("Accounting.Parameterization.Missing", $"{NombreDeModulo(module)} no tiene configurada {que}. Configúrela antes de continuar.");
    public static Error InstitutionalLinkMissing(string entidad) =>
        new("Accounting.Parameterization.InstitutionalLinkMissing", $"{entidad} no tiene persona vinculada como tercero (Personas). Vincúlela antes de continuar.");

    // ---- cuentas ----
    public static Error AccountNotFound(string code) => new("Accounting.Account.NotFound", $"La cuenta {code} no existe.");
    public static Error AccountCodeInvalid(string detalle) => new("Accounting.Account.CodeInvalid", detalle);
    public static Error AccountCodeDuplicate(string code) => new("Accounting.Account.CodeDuplicate", $"Ya existe la cuenta {code}.");
    public static Error AccountLevelNotAllowed(byte level, byte movement) =>
        new("Accounting.Account.LevelNotAllowed", $"La empresa trabaja con movimiento en nivel {movement}: no se crean cuentas de nivel {level}.");
    public static Error AccountLocked(DateOnly desde) =>
        new("Accounting.Account.Locked", $"La cuenta tiene movimientos desde el {desde:yyyy-MM-dd}: sólo puede cambiar el nombre e inactivarla.");
    public static readonly Error AccountFromCatalog = new("Accounting.Account.FromCatalog",
        "Las cuentas del catálogo (niveles 1 a 4) no se editan ni se eliminan; sólo se activan o inactivan.");
    public static readonly Error AccountHasMovements = new("Accounting.Account.HasMovements",
        "La cuenta tiene movimientos: no se elimina, inactívela.");
    public static Error AccountReferenced(IReadOnlyList<Accounts.ReferenciaDeCuenta> referencias) =>
        new ErrorConDatos("Accounting.Account.Referenced",
            $"La cuenta está parametrizada en: {string.Join("; ", referencias.Select(r => $"{r.Module} › {r.Where} ({r.Detail})"))}. Cambie esas parametrizaciones o inactívela.",
            new { references = referencias });
    public static readonly Error AccountHasChildren = new("Accounting.Account.HasChildren", "La cuenta tiene auxiliares debajo: elimine o mueva esas primero.");
    public static readonly Error AccountBankNotFound = new("Accounting.Account.BankNotFound", "El banco indicado no existe.");
    public static readonly Error AccountParentNotFound = new("Accounting.Account.ParentNotFound", "La cuenta padre no existe.");
    public static readonly Error AccountNotMovement = new("Accounting.Account.NotMovement",
        "Las reglas sólo se configuran en cuentas de movimiento.");

    // ---- configuración ----
    public static readonly Error SetupAlreadyInitialized = new("Accounting.Setup.AlreadyInitialized", "La contabilidad ya está iniciada.");
    public static Error SetupCatalogNotFound(string code) => new("Accounting.Setup.CatalogNotFound", $"No existe el catálogo {code}.");
    public static Error SetupLengthsInvalid(string detalle) => new("Accounting.Setup.LengthsInvalid", detalle);
    public static Error SetupLocked(int auxiliares, DateOnly? primerMovimiento) =>
        new ErrorConDatos("Accounting.Setup.Locked", primerMovimiento is null
            ? $"Ya existen {auxiliares} cuenta(s) auxiliar(es): el catálogo, el nivel y las longitudes no cambian."
            : $"Ya existen {auxiliares} auxiliar(es) y hay movimientos desde el {primerMovimiento:yyyy-MM-dd}: el catálogo, el nivel y las longitudes no cambian.",
            new { companyAccounts = auxiliares, firstMovementAt = primerMovimiento });
    public static readonly Error SetupBranchNotFound = new("Accounting.Setup.BranchNotFound", "La sucursal principal no existe o está inactiva.");
    public static readonly Error SetupResultAccountNotMovement = new("Accounting.Setup.ResultAccountNotMovement",
        "La cuenta de resultado del ejercicio debe ser una cuenta de movimiento activa.");

    // ---- catálogos ----
    public static Error CatalogInvalid(IReadOnlyList<Setup.ErrorDeFila> errores) =>
        new ErrorConDatos("Accounting.Catalog.Invalid",
            $"El archivo tiene {errores.Count} fila(s) con error (la primera, fila {errores[0].Row}: {errores[0].Message}). Corríjalas y vuelva a importar; no se guardó nada.",
            new { errors = errores });
    public static readonly Error CatalogNotFound = new("Accounting.Catalog.NotFound", "No existe ese catálogo.");
    public static readonly Error CatalogNotImported = new("Accounting.Catalog.NotImported", "Sólo los catálogos importados se pueden retirar.");
    public static readonly Error CatalogInUse = new("Accounting.Catalog.InUse", "Ese catálogo es el de la empresa: no se retira.");

    // ---- apertura ----
    public static readonly Error OpeningAlreadyExists = new("Accounting.Opening.AlreadyExists",
        "Ya hay una apertura contabilizada: reverse la existente antes de cargar otra.");
    public static Error OpeningInvalid(IReadOnlyList<Setup.ErrorDeFila> errores) =>
        new ErrorConDatos("Accounting.Opening.Invalid",
            $"El archivo de apertura tiene {errores.Count} fila(s) con error (la primera, fila {errores[0].Row}: {errores[0].Message}); no se guardó nada.",
            new { errors = errores });
    public static Error OpeningDateInvalid(DateOnly esperada) =>
        new("Accounting.Opening.DateInvalid", $"La apertura se fecha el día anterior al primer período: {esperada:yyyy-MM-dd}.");

    // ---- concurrencia ----
    public static readonly Error StaleRowVersion = Error.StaleRowVersion;

    private static ErrorDeLinea Linea(int line, string field, string code, string message, string? accountCode = null, Severidad severidad = Severidad.Error) =>
        new(line, field, code, message, severidad, accountCode);

    public static string NombreDeModulo(string module) => ModuloContable.Nombre(module);
}
