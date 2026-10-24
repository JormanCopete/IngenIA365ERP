namespace IngenIA365ERP.Application.Common.Integration.Inventory;

/// <summary>El efectivo de un documento confirmado del punto de venta: recibido y devuelto, con la persona y la oficina. (nuevo)</summary>
public sealed record EfectivoDelPuntoDeVentaDto(
    Guid DocumentPublicId,
    string DocumentNumber,
    DateOnly OperationDate,
    Guid PersonPublicId,
    decimal CashReceived,
    decimal CashRefunded,
    Guid? BranchPublicId);

/// <summary>
/// El efectivo del punto de venta de Comercial para el registro único de transacciones en efectivo de Recaudo (feature 015, R1, T0388; E-03; contratos-v2.md §4;
/// FR-204). Comercial lo ofrece y Recaudo lo suma a sus acumulados, señales y reporte <b>sin copiar las filas</b>: síncrono, de sólo lectura, en la unidad de trabajo de
/// quien llama. Sólo documentos confirmados con persona; lo anulado no cuenta. (nuevo)
/// </summary>
public interface IEfectivoDelPuntoDeVenta
{
    /// <summary>El efectivo de una persona en un mes.</summary>
    Task<IReadOnlyList<EfectivoDelPuntoDeVentaDto>> PorPersonaAsync(Guid personPublicId, int anio, int mes, CancellationToken ct);

    /// <summary>El efectivo de todas las personas entre dos fechas (para el reporte y las señales).</summary>
    Task<IReadOnlyList<EfectivoDelPuntoDeVentaDto>> PorPeriodoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
}
