namespace IngenIA365ERP.Application.Common.Integration.Inventory;

// Feature 015, T2 (fase 44, T0463; E-03; FR-133, FR-166 parte T2; contratos-v2.md §4): los lotes de datáfono que se cuentan al cerrar las sesiones de caja, para la
// conciliación de adquirentes de Tesorería. Recaudo ofrece los suyos por ILotesDeDatafonoDeRecaudo con el mismo registro.

/// <summary>Un lote de datáfono contado: el datáfono y su red, el número del lote, el día y su total. Sin datos personales. (nuevo)</summary>
public sealed record LoteDeDatafonoDto(Guid CardTerminalPublicId, string TerminalCode, Guid? AcquirerPublicId, string? AcquirerCode, string BatchNumber, DateOnly Date,
    decimal Amount);

/// <summary>
/// Ofrece Comercial → usa Tesorería (T2; E-03): los lotes de datáfono de los arqueos confirmados del punto de venta entre dos fechas (un lote contado en varios arqueos
/// cuenta una vez, el último). Síncrono, de sólo lectura. (nuevo)
/// </summary>
public interface ILotesDeDatafonoDelPuntoDeVenta
{
    Task<IReadOnlyList<LoteDeDatafonoDto>> PorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
}
