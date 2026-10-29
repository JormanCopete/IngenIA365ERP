namespace IngenIA365ERP.Application.Common.Integration.Receipts;

/// <summary>La referencia de pago estable de un producto, para imprimirla en un documento al asociado. (nuevo)</summary>
public sealed record ReferenciaDePagoDelProductoDto(Guid ProductPublicId, string Reference, string BaseNumber, string CheckDigit, string Algorithm);

/// <summary>
/// Ofrece Recaudo → usa Cartera (feature 015, R2, fase 38, T0436; FR-090; R-05; DT §2.7): la referencia de pago estable de un crédito o una cuenta —el número del
/// producto con su dígito de verificación— que los documentos al asociado (extracto, plan de pagos, cupón) pueden imprimir, sin que Cartera lea tablas <c>RCD_</c>.
/// Es de sólo lectura y determinista: la misma base da siempre la misma referencia; registrarla es de Recaudo. Nula si Recaudo no tiene el algoritmo configurado. (nuevo)
/// </summary>
public interface IReferenciasDePago
{
    Task<ReferenciaDePagoDelProductoDto?> DelProductoAsync(Guid productPublicId, string productNumber, CancellationToken ct);
}
