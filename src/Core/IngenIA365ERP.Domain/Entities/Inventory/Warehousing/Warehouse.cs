using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Warehousing;

/// <summary>
/// Bodega de una sucursal contable (<c>INV_Warehouses</c>; feature 012, T202; FR-032 a FR-034, FR-089 a FR-091;
/// data-model §2.2). <see cref="Code"/> es inmutable (dimensión <c>WarehouseCode</c> de la matriz, T27) y la sucursal
/// no cambia. <see cref="Behavior"/> <b>(nuevo)</b> es copia inmutable del de su tipo: la usa el índice de una sola bodega
/// de tránsito por sucursal y las reglas de clase sin unir tablas. Nace <see cref="WarehouseActivationStatus.NotActivated"/>:
/// sólo admite su saldo inicial hasta que <c>ActivateWarehouseCommand</c> (US4) la active.
/// </summary>
public class Warehouse : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>La sucursal contable (<c>COR_Branches</c>, la de la 009).</summary>
    public int BranchId { get; set; }

    public int WarehouseTypeId { get; set; }

    public WarehouseType? WarehouseType { get; set; }

    public WarehouseBehavior Behavior { get; set; } = WarehouseBehavior.Operational;

    // US4 (T306): las cuatro columnas de la activación sólo cambian por FijarFechaDeCorte y Activar. El «init» deja armarlas al
    // crear (semillas, pruebas); EF las lee y escribe por su campo (convención _nombre).
    private WarehouseActivationStatus _activationStatus = WarehouseActivationStatus.NotActivated;
    private DateOnly? _cutoffDate;
    private DateTime? _activatedAt;
    private int? _activatedByUserId;

    public WarehouseActivationStatus ActivationStatus { get => _activationStatus; init => _activationStatus = value; }

    /// <summary>La víspera de la activación: la fecha del saldo inicial.</summary>
    public DateOnly? CutoffDate { get => _cutoffDate; init => _cutoffDate = value; }

    public DateTime? ActivatedAt { get => _activatedAt; init => _activatedAt = value; }

    /// <summary><c>SEC_Users.Id</c> de quien la activó.</summary>
    public int? ActivatedByUserId { get => _activatedByUserId; init => _activatedByUserId = value; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<WarehouseLocation> Locations { get; set; } = new List<WarehouseLocation>();

    public bool EsTransito => Behavior == WarehouseBehavior.Transit;

    public bool EstaActiva => _activationStatus == WarehouseActivationStatus.Active;

    /// <summary>
    /// Fija la fecha de corte (US4, T306; FR-089): la del saldo inicial, la víspera de la activación. La misma fecha no cambia
    /// nada. Otra fecha se rechaza si la bodega ya está activa o si ya tiene un saldo inicial confirmado vigente
    /// (<paramref name="conSaldoConfirmado"/>, que conoce quien pregunta: la entidad no ve los documentos).
    /// </summary>
    /// <exception cref="InvalidOperationException">La bodega está activa o ya tiene su saldo confirmado con otra fecha.</exception>
    public void FijarFechaDeCorte(DateOnly corte, bool conSaldoConfirmado = false)
    {
        if (_cutoffDate == corte) return;
        if (EstaActiva)
            throw new InvalidOperationException($"La bodega {Code} ya está activa: su fecha de corte no cambia.");
        if (conSaldoConfirmado && _cutoffDate is not null)
            throw new InvalidOperationException($"La bodega {Code} ya tiene su saldo inicial confirmado al {_cutoffDate:yyyy-MM-dd}: se anula primero.");
        _cutoffDate = corte;
    }

    /// <summary>
    /// Activa la bodega (US4, T306; FR-090, FR-091): sólo desde <see cref="WarehouseActivationStatus.NotActivated"/>; sella el
    /// estado, la fecha de corte y quién y cuándo. Desde ahí sólo el módulo nuevo la opera.
    /// </summary>
    /// <exception cref="InvalidOperationException">Ya estaba activa.</exception>
    public void Activar(DateOnly corte, int usuarioId, DateTimeOffset ahora)
    {
        if (EstaActiva) throw new InvalidOperationException($"La bodega {Code} ya está activa.");
        _activationStatus = WarehouseActivationStatus.Active;
        _cutoffDate = corte;
        _activatedAt = ahora.UtcDateTime;
        _activatedByUserId = usuarioId;
    }
}
