using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Integration.Receipts;

// 2026-11-27 (feature 028, E4, T085; HU7, FR-026; research R14, E-16): el puerto que Recaudo ofrece y Cartera usa para recoger facturas a crédito de Comercial con
// el desembolso de un crédito nuevo. Las facturas son saldo de Contabilidad que sólo se cobra con un recibo de Recaudo; Cartera no lee tablas RCD_ ni el saldo de
// Contabilidad: todo pasa por aquí, dentro de la unidad de trabajo del desembolso. Como los puertos de escritura de Recaudo para Tesorería (CH16), trae
// TomarCerrojosAsync: el desembolso lo llama ANTES de sus propios cerrojos, para que el orden global (Recaudo antes que los contratos de Cartera) valga también en
// esta dirección. Sus cuatro métodos los fija LaCarteraSoloSeTocaPorSusInterfaces. (nuevo)

/// <summary>Una factura a crédito de Comercial pendiente de la persona, como la ve Recaudo: lo que informa Contabilidad menos lo que ya aplicaron sus recibos. (nuevo)</summary>
public sealed record FacturaPendienteEnRecaudo(string InvoiceNumber, Guid? AccountingDocumentPublicId, DateOnly Date, decimal Pending);

/// <summary>
/// Las facturas pendientes de una persona y si Recaudo puede emitir el recibo de la recogida: <see cref="NotReadyReason"/> nulo si puede; si no, por qué (falta el
/// tipo de documento de la clase «Recogida en el desembolso» o el medio del cruce interno). (nuevo)
/// </summary>
public sealed record FacturasParaRecoger(IReadOnlyList<FacturaPendienteEnRecaudo> Invoices, string? NotReadyReason)
{
    public static FacturasParaRecoger Ninguna { get; } = new([], null);
}

/// <summary>Una factura que el desembolso recoge: su número (documento cruce), el valor que se le aplica y la clave de su porción (<c>CAR:{desembolso:N}:{factura}</c>). (nuevo)</summary>
public sealed record FacturaARecoger(string InvoiceNumber, decimal Amount, string Key);

/// <summary>
/// Lo que pide el desembolso: la clave del recibo (una por desembolso), el deudor que paga, la oficina del crédito nuevo, la fecha valor, las facturas, el número
/// de la operación del desembolso y el del crédito nuevo (la referencia del medio). (nuevo)
/// </summary>
public sealed record SolicitudDeRecogidaEnRecaudo(
    string Key,
    Guid DebtorPersonPublicId,
    Guid BranchPublicId,
    DateOnly ValueDate,
    IReadOnlyList<FacturaARecoger> Invoices,
    string DisbursementNumber,
    string? NewLoanNumber);

/// <summary>Una factura recogida: su número, lo que se le aplicó y la clave de su porción. (nuevo)</summary>
public sealed record FacturaRecogidaEnRecaudo(string InvoiceNumber, decimal Applied, string Key);

/// <summary>El recibo sin caja que recogió las facturas: su <c>PublicId</c>, su número y lo aplicado a cada una. (nuevo)</summary>
public sealed record ReciboDeLaRecogida(Guid ReceiptPublicId, string Number, IReadOnlyList<FacturaRecogidaEnRecaudo> Invoices);

/// <summary>La anulación del recibo de la recogida (al anular el crédito nuevo antes del giro): el recibo y su documento de anulación. (nuevo)</summary>
public sealed record ReciboDeLaRecogidaAnulado(Guid ReceiptPublicId, Guid VoidingPublicId, string VoidingNumber);

/// <summary>
/// Ofrece Recaudo → usa Cartera (feature 028, E4): la recogida de facturas a crédito de Comercial en el desembolso de un crédito nuevo. (nuevo)
/// </summary>
public interface IRecogidaEnRecaudo
{
    /// <summary>Las facturas a crédito de Comercial pendientes de <paramref name="persona"/> y si Recaudo puede emitir el recibo de la recogida. Sólo lee.</summary>
    Task<FacturasParaRecoger> PendientesAsync(Guid persona, CancellationToken ct);

    /// <summary>
    /// Antes de los cerrojos de Cartera: la configuración de Recaudo (compartida) y el pagador <paramref name="persona"/>; con <paramref name="recibos"/>, también
    /// esos recibos (los que se van a anular). Exige la transacción del que llama.
    /// </summary>
    Task TomarCerrojosAsync(Guid persona, IReadOnlyList<Guid> recibos, CancellationToken ct);

    /// <summary>
    /// Emite el recibo sin caja que aplica cada factura por su valor, en la transacción del que llama y con sus cerrojos ya tomados, y su <c>ReciboEmitido</c>. Una
    /// factura que ya no existe o cuyo saldo no alcanza, o un recibo que no se puede emitir, es una falla: el desembolso no existe.
    /// </summary>
    Task<Result<ReciboDeLaRecogida>> AplicarAsync(SolicitudDeRecogidaEnRecaudo solicitud, CancellationToken ct);

    /// <summary>
    /// Anula el recibo <paramref name="recibo"/> de una recogida (sin aprobación: la anulación del crédito nuevo ya la tiene) con el espejo exacto de su asiento —no
    /// entró dinero— y su <c>ReciboAnulado</c>; la factura vuelve a quedar con saldo. Repetido, devuelve la anulación que ya tiene.
    /// </summary>
    Task<Result<ReciboDeLaRecogidaAnulado>> ReversarAsync(Guid recibo, string motivo, CancellationToken ct);
}
