using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Transfers;

/// <summary>
/// Los códigos de error de los traslados y del movimiento entre ubicaciones (feature 012, US10; contracts/api.md §10, §11;
/// decisiones-transversales §2.17), con el mensaje en español y el <c>data</c> que la persona necesita para corregir. Todos
/// responden 422. (nuevo)
/// </summary>
public static class ErroresDeTraslados
{
    /// <summary>El campo de la bodega de destino, para <c>Inventory.Document.FieldRequired</c>.</summary>
    public const string CampoDestino = "destinationWarehouse";

    /// <summary>El campo de la ubicación de destino de un movimiento entre ubicaciones.</summary>
    public const string CampoUbicacionDeDestino = "toLocation";

    public static Error SameWarehouse(string warehouseCode) => new ErrorConDatos("Inventory.Transfer.SameWarehouse",
        $"El origen y el destino son la misma bodega ({warehouseCode}). Para moverla dentro de la bodega use un movimiento entre ubicaciones.",
        new { warehouseCode });

    /// <summary>La sucursal del origen no tiene bodega de tránsito (nuevo): se crea con la primera bodega de la sucursal.</summary>
    public static Error TransitWarehouseMissing(string warehouseCode) => new ErrorConDatos("Inventory.Transfer.TransitWarehouseMissing",
        $"La sucursal de la bodega {warehouseCode} no tiene bodega de tránsito. Revise Inventario › Bodegas.",
        new { warehouseCode });

    public static Error ReceiveExceedsDispatched(int lineNumber, decimal dispatchedBase) => new ErrorConDatos(
        "Inventory.Transfer.ReceiveExceedsDispatched",
        $"Línea {lineNumber}: se recibe más de lo despachado ({dispatchedBase.ToString("0.####", CultureInfo.InvariantCulture)}). Lo que llegó de más se declara como sobrante.",
        new { lineNumber, dispatchedBase });

    public static Error AlreadyReceived(Guid receiptPublicId, string? displayNumber) => new ErrorConDatos("Inventory.Transfer.AlreadyReceived",
        $"El traslado ya se recibió en {displayNumber ?? "otra recepción"}. Lo que falta se resuelve desde sus diferencias.",
        new { receiptPublicId, displayNumber });

    public static Error NotInTransit(DocumentStatus status) => new ErrorConDatos("Inventory.Transfer.NotInTransit",
        "El despacho no está en tránsito: sólo se recibe un despacho confirmado y no anulado.",
        new { status = status.ToString() });

    public static Error ReceiptBeforeDispatch(DateOnly dispatchDate) => new ErrorConDatos("Inventory.Transfer.ReceiptBeforeDispatch",
        $"La recepción no puede ser anterior al despacho ({dispatchDate:dd/MM/yyyy}).",
        new { dispatchDate = dispatchDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });

    public static Error UseReverseTransfer() => new("Inventory.Transfer.UseReverseTransfer",
        "Una recepción de traslado no se anula: lo recibido se corrige con un traslado en sentido contrario.");

    /// <summary>Una línea de la recepción que no es del despacho.</summary>
    public static Error DispatchLineUnknown(Guid dispatchLinePublicId) => new ErrorConDatos("Validation.Invalid",
        "Una de las líneas no es del despacho que se recibe.",
        new { dispatchLinePublicId });

    // --------------------------------------------------------------------------------------- diferencias --

    public static Error DiscrepancyNotFound() => new("Inventory.TransferDiscrepancy.NotFound", "La diferencia de traslado no existe.");

    public static Error DiscrepancyNotPending(string state) => new ErrorConDatos("Inventory.TransferDiscrepancy.NotPending",
        state == Domain.Entities.Inventory.Documents.TransferDiscrepancy.EstadoResuelta
            ? "La diferencia ya está resuelta."
            : "La diferencia ya tiene una resolución en aprobación.",
        new { state });

    public static Error ResolutionNotAllowed(TransferDiscrepancyKind kind, IReadOnlyList<TransferDiscrepancyResolution> allowed) => new ErrorConDatos(
        "Inventory.TransferDiscrepancy.ResolutionNotAllowed",
        kind == TransferDiscrepancyKind.Shortage
            ? "Un faltante se resuelve devolviéndolo al origen, dándolo de baja desde el tránsito o recibiéndolo tarde."
            : "Un sobrante se resuelve con un ajuste positivo en el destino.",
        new { kind = kind.ToString(), allowed = allowed.Select(a => a.ToString()).ToList() });

    public static Error QuantityExceeds(decimal pendingBase) => new ErrorConDatos("Inventory.TransferDiscrepancy.QuantityExceeds",
        $"La cantidad supera lo pendiente de la diferencia ({pendingBase.ToString("0.####", CultureInfo.InvariantCulture)}).",
        new { pendingBase });

    /// <summary>La causa no sirve para esa salida (nuevo): la baja desde el tránsito y el sobrante tienen causas propias.</summary>
    public static Error CauseNotAllowed(string causeCode, TransferDiscrepancyResolution resolution) => new ErrorConDatos(
        "Inventory.TransferDiscrepancy.CauseNotAllowed",
        resolution == TransferDiscrepancyResolution.WriteOffFromTransit
            ? $"La causa {causeCode} no admite bajas desde el tránsito (daño, hurto, reclamación al transportador…)."
            : $"La causa {causeCode} no admite entradas por ajuste.",
        new { causeCode, resolution = resolution.ToString() });

    // --------------------------------------------------------------------------------- entre ubicaciones --

    public static Error LocationSame(int lineNumber, string productCode) => new ErrorConDatos("Inventory.Location.Same",
        $"Línea {lineNumber}: {productCode} sale y entra a la misma ubicación.",
        new { lineNumber, productCode });
}
