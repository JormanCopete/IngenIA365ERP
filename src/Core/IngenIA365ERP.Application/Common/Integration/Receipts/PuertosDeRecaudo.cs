namespace IngenIA365ERP.Application.Common.Integration.Receipts;

// Feature 015, T0040 (R-05; FR-003; contracts/interfaces.md §2; data-model §0.5, CH16): los puertos que Recaudo ofrece y Tesorería usa. Los dos
// de escritura (IPartidasDesdeConciliacion, IResultadoDeOrdenesDeRecaudo) tienen TomarCerrojosAsync: Tesorería lo llama ANTES de sus propios
// cerrojos, para que el orden global Recaudo → Tesorería valga también en esa dirección (LosCerrojosDeRecaudoYTesoreriaVanEnOrden).

/// <summary>
/// Un registro de Recaudo que se cruza en la conciliación de la cuenta: el recibo de banco y la partida por identificar (los dos son abonos). Lleva la línea del extracto
/// de la que nació, si la tiene, para que el cruce la reconozca por su vínculo (US25, fase 17; nuevo, DT §2.7). (nuevo)
/// </summary>
public sealed record RegistroConciliableDto(
    Guid SourcePublicId,
    string SourceKind,
    string Number,
    DateOnly Date,
    decimal Amount,
    string? Reference,
    Guid? StatementLinePublicId = null);

/// <summary>La partida por identificar que nació de un abono sin registro en la conciliación (US25-2). (nuevo)</summary>
public sealed record PartidaCreadaDto(Guid UnidentifiedItemPublicId, string Number);

/// <summary>Ofrece Recaudo → usa Tesorería (T0): los registros de Recaudo de una cuenta propia para la conciliación. (nuevo)</summary>
public interface IRecaudoParaConciliacion
{
    Task<IReadOnlyList<RegistroConciliableDto>> RegistrosAsync(Guid ownBankAccountPublicId, DateOnly desde, DateOnly hasta, CancellationToken ct);
}

/// <summary>Ofrece Recaudo → usa Tesorería (T0): un abono sin registro se vuelve partida por identificar. (nuevo)</summary>
public interface IPartidasDesdeConciliacion
{
    /// <summary>Antes de los cerrojos de Tesorería: toma <c>RCD_Setup</c> compartido (la partida nueva no tiene fila que tomar).</summary>
    Task TomarCerrojosAsync(Guid ownBankAccountPublicId, CancellationToken ct);

    Task<PartidaCreadaDto> CrearAsync(Guid statementLinePublicId, string motivo, CancellationToken ct);
}

/// <summary>Ofrece Recaudo → usa Tesorería (T0): el resultado de una orden de pago que pidió Recaudo (girada, rechazada, cheque sin cobrar, anulada). (nuevo)</summary>
public interface IResultadoDeOrdenesDeRecaudo
{
    /// <summary>Antes de los cerrojos de Tesorería: <c>RCD_Setup</c> compartido y las filas de origen de esas órdenes, por Id.</summary>
    Task TomarCerrojosAsync(IReadOnlyList<Guid> paymentOrderPublicIds, CancellationToken ct);

    Task InformarAsync(Guid paymentOrderPublicId, string outcome, DateOnly occurredOn, string? reason, CancellationToken ct);
}
