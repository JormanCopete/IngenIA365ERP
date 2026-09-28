using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Cash;

/// <summary>
/// Los errores y avisos de la caja: sesiones, arqueo, movimientos y cierre del día (feature 012, I3, T617–T620; contracts/api.md §21.5).
/// <c>Inventory.CashSession.NotOpen</c> es el de <c>ErroresDelPos</c> (lo comparten el POS y la caja); <c>RegisterBusy</c>,
/// <c>CashierBusy</c> y <c>Inventory.DayClose.AlreadyClosed</c> los arma <c>ColisionesDeVenta</c>, que también traduce la carrera.
/// <c>Inventory.CashSession.{NotFound, BaseRequired, CashMeansMissing}</c>, <c>Inventory.CashMovement.Invalid</c> e
/// <c>Inventory.DayClose.{NotFound, NotClosed}</c> son (nuevo). Una sesión ajena sin <c>Inventory.CashSessions.ViewAll</c> es el mismo
/// 404 que la inexistente. (nuevo)
/// </summary>
public static class ErroresDeCaja
{
    public const string SessionNotFoundCode = "Inventory.CashSession.NotFound";
    public const string CashierWithoutPersonCode = "Inventory.CashSession.CashierWithoutPerson";
    public const string DayClosedCode = "Inventory.CashSession.DayClosed";
    public const string HasOpenDraftsCode = "Inventory.CashSession.HasOpenDrafts";
    public const string HasPendingMovementsCode = "Inventory.CashSession.HasPendingMovements";
    public const string ReasonRequiredCode = "Inventory.CashSession.ReasonRequired";
    public const string CountMethodMismatchCode = "Inventory.CashSession.CountMethodMismatch";
    public const string NothingToRecountCode = "Inventory.CashSession.NothingToRecount";
    public const string BaseDiffersCode = "Inventory.CashSession.BaseDiffers";
    public const string BaseRequiredCode = "Inventory.CashSession.BaseRequired";
    public const string CashMeansMissingCode = "Inventory.CashSession.CashMeansMissing";
    public const string MovementSessionClosedCode = "Inventory.CashMovement.SessionClosed";
    public const string DestinationRegisterClosedCode = "Inventory.CashMovement.DestinationRegisterClosed";
    public const string ExceedsExpectedCode = "Inventory.CashMovement.ExceedsExpected";
    public const string MovementInvalidCode = "Inventory.CashMovement.Invalid";
    public const string DayCloseSessionsOpenCode = "Inventory.DayClose.SessionsOpen";
    public const string DayCloseAlreadyClosedCode = "Inventory.DayClose.AlreadyClosed";
    public const string DayCloseNotFoundCode = "Inventory.DayClose.NotFound";
    public const string DayCloseNotClosedCode = "Inventory.DayClose.NotClosed";

    /// <summary>La sesión no existe, está fuera del alcance o es de otro cajero y quien pregunta no tiene <c>ViewAll</c> (404).</summary>
    public static Error SessionNotFound() => new(SessionNotFoundCode, "La sesión de caja no existe.");

    /// <summary>Con el faltante a cargo del cajero, el usuario tiene que ser una persona (422, §21.1).</summary>
    public static Error CashierWithoutPerson() => new(CashierWithoutPersonCode,
        "El faltante de arqueo de este punto va a cargo del cajero y su usuario no está vinculado a una persona: vincúlelo en " +
        "Seguridad › Usuarios (campo «Persona») antes de abrir la caja.");

    /// <summary>El día ya se cerró en el punto: no se abren sesiones con esa fecha operativa (422, §21.1).</summary>
    public static Error DayClosed(DateOnly operatingDate) => new ErrorConDatos(DayClosedCode,
        $"El día {operatingDate:yyyy-MM-dd} ya se cerró en este punto de venta: pida reabrirlo para abrir otra sesión.",
        new { operatingDate });

    /// <summary>La fecha operativa, sin base del día (422). (nuevo)</summary>
    public static Error BaseRequired() => new ErrorConDatos(BaseRequiredCode,
        "Esta caja abre con base del día: indique la base con que empieza.", new { field = "openingBase" });

    /// <summary>No hay un medio de clase efectivo activo al que entre la base (422). (nuevo)</summary>
    public static Error CashMeansMissing() => new(CashMeansMissingCode,
        "No hay un medio de pago de clase efectivo activo: créelo en Maestros › Medios de pago.");

    /// <summary>Ventas suspendidas o borradores abiertos en la sesión (422, §21.2).</summary>
    public static Error HasOpenDrafts(IReadOnlyList<BorradorAbiertoDto> drafts) => new ErrorConDatos(HasOpenDraftsCode,
        $"La sesión tiene {drafts.Count} venta(s) sin terminar: cóbrelas, descártelas o recupérelas antes de cerrar.", new { drafts });

    /// <summary>Movimientos de caja en borrador o en aprobación (422, §21.2).</summary>
    public static Error HasPendingMovements(IReadOnlyList<MovimientoPendienteDto> movements) => new ErrorConDatos(HasPendingMovementsCode,
        $"La sesión tiene {movements.Count} movimiento(s) de caja sin terminar: confírmelos, descártelos o anúlelos antes de cerrar.",
        new { movements });

    /// <summary>Medios con diferencia y sin motivo (422, §21.2).</summary>
    public static Error ReasonRequired(IReadOnlyList<string> paymentMeansCodes) => new ErrorConDatos(ReasonRequiredCode,
        $"Explique la diferencia de {string.Join(", ", paymentMeansCodes)}: toda diferencia distinta de cero lleva su motivo.",
        new { paymentMeansCodes });

    /// <summary>Un dato que no corresponde a cómo se cuenta el medio (422, §21.2).</summary>
    public static Error CountMethodMismatch(string paymentMeansCode, CashCountMethod countMethod) => new ErrorConDatos(CountMethodMismatchCode,
        countMethod switch
        {
            CashCountMethod.PhysicalCount => $"{paymentMeansCode} se cuenta por denominaciones o por su total.",
            CashCountMethod.VoucherTotal => $"{paymentMeansCode} se cuenta con el lote de cierre de cada datáfono.",
            CashCountMethod.ByReference => $"{paymentMeansCode} se cuenta marcando cada referencia.",
            _ => $"{paymentMeansCode} no se cuenta: su esperado es lo contado.",
        },
        new { paymentMeansCode, countMethod });

    /// <summary>No hay una diferencia rechazada que recontar (422, §21.2).</summary>
    public static Error NothingToRecount() => new(NothingToRecountCode,
        "No hay nada que recontar: la diferencia de esta sesión no fue rechazada.");

    /// <summary>El aviso de una base distinta del fondo fijo (no es error, §21.1).</summary>
    public static AvisoDto BaseDiffers(decimal openingBase, decimal fund) => new(BaseDiffersCode,
        $"La caja trabaja con fondo fijo de {fund:N2}; la base indicada ({openingBase:N2}) es sólo informativa.",
        new { openingBase, fund });

    /// <summary>Anular un movimiento con su sesión ya cerrada (422, §21.3).</summary>
    public static Error MovementSessionClosed() => new(MovementSessionClosedCode,
        "La sesión del movimiento ya se cerró: el movimiento sólo se anula con la sesión abierta.");

    /// <summary>La caja destino no tiene sesión abierta (422, §21.3).</summary>
    public static Error DestinationRegisterClosed(string cashRegisterCode) => new ErrorConDatos(DestinationRegisterClosedCode,
        $"La caja {cashRegisterCode} no tiene una sesión abierta que reciba el dinero.", new { cashRegisterCode });

    /// <summary>El retiro supera lo esperado del medio (422, §21.3).</summary>
    public static Error ExceedsExpected(string paymentMeansCode, decimal expected) => new ErrorConDatos(ExceedsExpectedCode,
        $"El retiro supera lo que la sesión tiene en {paymentMeansCode} ({expected:N2}).", new { paymentMeansCode, expected });

    /// <summary>Un campo del movimiento que su clase exige o no admite (422). (nuevo)</summary>
    public static Error MovementInvalid(string field, string message) => new ErrorConDatos(MovementInvalidCode, message, new { field });

    /// <summary>Sesiones abiertas del punto con esa fecha (422, §21.4).</summary>
    public static Error DayCloseSessionsOpen(IReadOnlyList<SesionAbiertaDto> sessions) => new ErrorConDatos(DayCloseSessionsOpenCode,
        $"Hay {sessions.Count} sesión(es) abierta(s) en el punto con esa fecha: ciérrelas antes de cerrar el día.", new { sessions });

    /// <summary>El cierre del día no existe o está fuera del alcance (404). (nuevo)</summary>
    public static Error DayCloseNotFound() => new(DayCloseNotFoundCode, "El cierre del día no existe.");

    /// <summary>Se pidió reabrir un cierre ya reabierto (422). (nuevo)</summary>
    public static Error DayCloseNotClosed() => new(DayCloseNotClosedCode, "Ese cierre del día ya fue reabierto: el vigente es el más reciente.");
}

/// <summary>Una venta sin terminar en <c>Inventory.CashSession.HasOpenDrafts</c>. (nuevo)</summary>
public sealed record BorradorAbiertoDto(Guid DraftPublicId, string? Label, decimal Total);

/// <summary>Un movimiento sin terminar en <c>Inventory.CashSession.HasPendingMovements</c>. (nuevo)</summary>
public sealed record MovimientoPendienteDto(Guid DocumentPublicId, CashMovementKind Kind, decimal Amount, DocumentStatus Status);

/// <summary>Una sesión que impide cerrar el día (<c>Inventory.DayClose.SessionsOpen</c>). (nuevo)</summary>
public sealed record SesionAbiertaDto(Guid CashSessionPublicId, string CashRegisterCode, string CashierName);
