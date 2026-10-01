using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// El turno de un cajero en una caja (<c>INV_CashSessions</c>; feature 012, I3, T574; T50; data-model §15). Reemplaza a los turnos
/// del módulo anterior y separa el estado del día de la configuración del punto. Lo que se <b>sella</b> al abrir no cambia
/// (<c>init</c>): el modo de base, el tratamiento del faltante, el arqueo ciego y si el cajero es exclusivo —la columna sellada
/// deja fijo el índice <c>UK_INV_CashSessions_Cashier_Open</c> aunque el parámetro cambie—. Una sesión abierta por caja y, con
/// <see cref="ExclusiveCashier"/>, una por cajero. <see cref="LastActivityAt"/> se escribe en cada cobro y movimiento antes del
/// cerrojo (data-model §15 «Concurrencia»): un cierre espera al cobro en curso o el cobro llega tarde y responde
/// <c>Inventory.CashSession.NotOpen</c>.
/// </summary>
public class CashSession : AuditableEntity
{
    /// <summary>Los valores sellados de <c>Caja.BaseModo</c>.</summary>
    public const string BaseFondoFijo = "FondoFijo";

    /// <inheritdoc cref="BaseFondoFijo"/>
    public const string BaseDelDia = "BaseDelDia";

    public int CashRegisterId { get; init; }

    public CashRegister? CashRegister { get; set; }

    /// <summary>Copia desnormalizada (alcance, cierre del día).</summary>
    public int PointOfSaleId { get; init; }

    /// <summary><c>SEC_Users.Id</c> de <c>IActorActual</c>, nunca el entero del token.</summary>
    public int CashierUserId { get; init; }

    /// <summary>Copia de <c>SEC_Users.PersonId</c> al abrir; obligatoria si el faltante va a cargo del cajero.</summary>
    public int? CashierPersonId { get; init; }

    public string CashierName { get; init; } = string.Empty;

    /// <summary>Rótulo opcional («mañana», «tarde»).</summary>
    public string? Label { get; set; }

    /// <summary>Fecha local de la apertura (T20); la fecha fiscal de cada venta es la real.</summary>
    public DateOnly OperatingDate { get; init; }

    public DateTime OpenedAt { get; init; }

    /// <summary>Copia de <c>Caja.BaseModo</c>: <see cref="BaseFondoFijo"/> o <see cref="BaseDelDia"/>.</summary>
    public string BaseMode { get; init; } = BaseFondoFijo;

    /// <summary>Con fondo fijo, la base que dejó la sesión anterior de la caja; con base del día la base llega por su movimiento.</summary>
    public decimal OpeningBase { get; init; }

    /// <summary>El movimiento <c>BaseIncome</c> de la apertura, si lo hubo.</summary>
    public int? BaseIncomeDocumentId { get; set; }

    /// <summary>Copia de <c>Caja.TratamientoFaltante</c> (<c>Gasto</c> | <c>CargoAlCajero</c>) con ámbito punto.</summary>
    public string ShortageTreatment { get; init; } = string.Empty;

    /// <summary>Copia de <c>Caja.ArqueoCiego</c>.</summary>
    public bool IsBlindCount { get; init; }

    /// <summary>Copia de <c>Caja.UnaSesionPorCajero</c>.</summary>
    public bool ExclusiveCashier { get; init; }

    public CashSessionStatus Status { get; private set; } = CashSessionStatus.Open;

    public DateTime LastActivityAt { get; set; }

    public DateTime? ClosedAt { get; private set; }

    public int? ClosedByUserId { get; private set; }

    public bool EstaAbierta => Status == CashSessionStatus.Open;

    /// <summary>Cierra la sesión (inmediato: la caja puede abrir otra). Una sesión cerrada no se reabre.</summary>
    public void Cerrar(int cerradaPor, DateTime ahoraUtc)
    {
        if (Status != CashSessionStatus.Open)
            throw new InvalidOperationException($"La sesión {Id} ya está cerrada.");
        Status = CashSessionStatus.Closed;
        ClosedAt = ahoraUtc;
        ClosedByUserId = cerradaPor;
        LastActivityAt = ahoraUtc;
    }
}
