using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Inventory.GoLive;

/// <summary>
/// Los códigos de la puesta en marcha (feature 012, US4; contracts/api.md §13.1–§13.3; decisiones-transversales §2.17): saldo
/// inicial, cifras de SOLIDO y activación de bodegas. Al pie de la letra del contrato; <c>data</c> trae lo que hace falta para
/// corregir. (nuevo)
/// </summary>
public static class GoLiveErrors
{
    // ---------------------------------------------------------------------------------------- saldo inicial --

    public const string OpeningBalanceWarehouseActiveCode = "Inventory.OpeningBalance.WarehouseActive";
    public const string OpeningBalanceTransitNotAllowedCode = "Inventory.OpeningBalance.TransitNotAllowed";
    public const string OpeningBalanceAlreadyConfirmedCode = "Inventory.OpeningBalance.AlreadyConfirmed";

    /// <summary><b>(nuevo)</b> Aviso de fila: costo unitario cero (se admite; plantillas §14).</summary>
    public const string OpeningBalanceZeroCostCode = "Inventory.OpeningBalance.ZeroCost";

    /// <summary>El saldo inicial sólo entra a una bodega no activa (FR-091).</summary>
    public static Error OpeningBalanceWarehouseActive(Guid warehousePublicId, string warehouseCode) => new ErrorConDatos(OpeningBalanceWarehouseActiveCode,
        $"La bodega {warehouseCode} ya está activa: su saldo inicial no se carga ni se anula; lo que falte se ajusta.",
        new { warehousePublicId, warehouseCode });

    public static Error OpeningBalanceTransitNotAllowed(string warehouseCode) => new ErrorConDatos(OpeningBalanceTransitNotAllowedCode,
        $"La bodega {warehouseCode} es de tránsito: no lleva saldo inicial.", new { warehouseCode });

    /// <summary>Un documento de saldo inicial ya confirmado de la bodega (para anularlo primero).</summary>
    public sealed record SaldoConfirmado(Guid PublicId, string? DisplayNumber, DateOnly CutoffDate);

    public static Error OpeningBalanceAlreadyConfirmed(string warehouseCode, IReadOnlyList<SaldoConfirmado> documents) => new ErrorConDatos(
        OpeningBalanceAlreadyConfirmedCode,
        $"La bodega {warehouseCode} ya tiene su saldo inicial confirmado ({string.Join(", ", documents.Select(d => d.DisplayNumber ?? "sin número"))}): anúlelo primero.",
        new { warehouseCode, documents });

    // ------------------------------------------------------------------------------------ cifras de SOLIDO --

    /// <summary>Aviso de fila: el código de producto de SOLIDO no está en el catálogo nuevo (queda sin resolver).</summary>
    public const string LegacyFiguresCodeUnresolvedCode = "Inventory.LegacyFigures.CodeUnresolved";

    /// <summary><b>(nuevo)</b> Aviso de fila: el grupo del archivo no es el del producto a la fecha de la cifra.</summary>
    public const string LegacyFiguresGroupMismatchCode = "Inventory.LegacyFigures.GroupMismatch";

    // -------------------------------------------------------------------------------------------- activación --

    public const string ActivationOpeningBalanceNotConfirmedCode = "Inventory.Activation.OpeningBalanceNotConfirmed";
    public const string ActivationCutoffMismatchCode = "Inventory.Activation.CutoffMismatch";
    public const string ActivationAccountingUnavailableCode = "Inventory.Activation.AccountingUnavailable";
    public const string ActivationAlreadyActiveCode = "Inventory.Activation.AlreadyActive";
    public const string ActivationDifferenceCode = "Inventory.Activation.Difference";
    public const string ActivationAcceptDifferenceNotAllowedCode = "Inventory.Activation.AcceptDifferenceNotAllowed";

    public static Error ActivationOpeningBalanceNotConfirmed(string warehouseCode, IReadOnlyList<object> documents) => new ErrorConDatos(
        ActivationOpeningBalanceNotConfirmedCode,
        $"La bodega {warehouseCode} tiene saldo inicial sin confirmar: confírmelo (o descártelo) antes de activarla.",
        new { warehouseCode, documents });

    public static Error ActivationCutoffMismatch(string warehouseCode, DateOnly cutoffDate, DateOnly openingBalanceDate) => new ErrorConDatos(
        ActivationCutoffMismatchCode,
        $"La fecha de corte pedida ({cutoffDate:yyyy-MM-dd}) no es la del saldo inicial de la bodega {warehouseCode} ({openingBalanceDate:yyyy-MM-dd}).",
        new { warehouseCode, cutoffDate, openingBalanceDate });

    public static Error ActivationAccountingUnavailable() => new(ActivationAccountingUnavailableCode,
        "Contabilidad todavía no responde la comparación de saldos por conjunto de cuentas: la bodega no se puede comparar con los libros.");

    public static Error ActivationAlreadyActive(string warehouseCode, DateOnly? cutoffDate) => new ErrorConDatos(ActivationAlreadyActiveCode,
        $"La bodega {warehouseCode} ya está activa.", new { warehouseCode, cutoffDate });

    public static Error ActivationDifference(object sets, decimal totalDifference) => new ErrorConDatos(ActivationDifferenceCode,
        $"El inventario de la bodega no cuadra con Contabilidad (diferencia {totalDifference:N2}). Para activarla igual, acepte la diferencia con motivo.",
        new { sets, totalDifference });

    public static Error ActivationAcceptDifferenceNotAllowed(string permissionCode) => new ErrorConDatos(ActivationAcceptDifferenceNotAllowedCode,
        $"Aceptar la diferencia al activar una bodega exige el permiso {permissionCode}.", new { permissionCode });
}
