namespace IngenIA365ERP.Domain.Enums.Lending;

// 2026-11-25 (pedido del dueño; feature 026): los cargos masivos de Cartera (LND_BulkChargeRuns, LND_BulkChargeLines). (nuevo)

/// <summary>
/// El estado de una corrida de cargos masivos (feature 026):
/// <code>
/// Draft ──confirmar──▶ Queued ──el conductor la toma──▶ Running ──sin pendientes──▶ Completed
/// Draft ──descartar──▶ Discarded;   Completed ──anular (motivo)──▶ Voiding ──▶ Voided (o Completed si algo no se pudo anular)
/// </code>
/// Sólo el borrador se cambia, se recalcula o se descarta. (nuevo)
/// </summary>
public enum BulkChargeRunStatus
{
    Draft = 1,
    Queued = 2,
    Running = 3,
    Completed = 4,
    Voiding = 5,
    Voided = 6,
    Discarded = 7,
}

/// <summary>El resultado de un asociado en la corrida. <c>Pending</c> y <c>Created</c> «ocupan» el período (unicidad por línea, asociado y período). (nuevo)</summary>
public enum BulkChargeLineStatus
{
    Pending = 1,
    Created = 2,
    Skipped = 3,
    Failed = 4,
    Voided = 5,
}

/// <summary>De dónde salen los asociados de la corrida: los filtros sobre los asociados activos o un plano. (nuevo)</summary>
public enum BulkChargeSource
{
    Filters = 1,
    File = 2,
}

/// <summary>Cómo se elige la forma de pago: una fija para todos o la de la ficha de cada asociado (con la de defecto de la línea si no la admite). (nuevo)</summary>
public enum BulkChargeChannelMode
{
    Fixed = 1,
    FromAssociate = 2,
}

/// <summary>El tercero de la contrapartida del cargo: el mismo asociado (deudor) o una persona elegida (un proveedor). (nuevo)</summary>
public enum BulkChargeCounterpart
{
    Debtor = 1,
    Person = 2,
}
