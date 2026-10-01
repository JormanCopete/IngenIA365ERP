using System.Text.RegularExpressions;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Sales.Payments;

/// <summary>
/// La copia de un medio de pago que el validador necesita (las columnas de <c>COR_PaymentMeans</c> que deciden la captura). La
/// aplicación la arma del medio vigente; el pago guarda su propia copia al confirmar. (nuevo)
/// </summary>
public sealed record MedioDePagoCopia(
    int PaymentMeansId,
    string Code,
    PaymentMeansClass Class,
    bool AllowsChange,
    bool AllowsPartial,
    bool RequiresReference,
    PaymentReferenceKind? ReferenceKind,
    byte? ReferenceMinLength,
    byte? ReferenceMaxLength,
    bool UniqueReference,
    CashCountMethod CountMethod);

/// <summary>Un pago que llega en el cuerpo (<c>DocumentPaymentInput</c>, contracts/api.md §22.4), ya con su medio resuelto. (nuevo)</summary>
public sealed record PagoPropuesto(
    MedioDePagoCopia Medio,
    decimal Amount,
    decimal? Tendered = null,
    string? Reference = null,
    string? AuthorizationCode = null,
    string? Last4 = null,
    string? TerminalBatchNumber = null,
    int? CashSessionId = null);

/// <summary>
/// Lo que se valida: el total a pagar (<c>AmountDue</c> = total − retenciones sufridas, T26), los pagos en el orden en que se
/// agregaron y las sesiones de caja abiertas del usuario. (nuevo)
/// </summary>
public sealed record PedidoDeCobro(decimal AmountDue, IReadOnlyList<PagoPropuesto> Pagos, IReadOnlyCollection<int> OpenCashSessionIds);

/// <summary>Un error con el código de contracts/api.md §22.5, el pago al que se refiere (índice) y su <c>data</c>. (nuevo)</summary>
public sealed record ErrorDePago(string Code, int? PaymentIndex, IReadOnlyDictionary<string, object?> Data);

/// <summary>Un pago que pasó: sus vueltas y su referencia normalizada (la de <c>INV_VoucherRedemptions</c>). (nuevo)</summary>
public sealed record PagoValidado(int Index, decimal Amount, decimal? Tendered, decimal Change, string? NormalizedReference);

/// <summary>El veredicto: errores, pagos, lo pagado, lo que falta o sobra, y si la venta queda a crédito. (nuevo)</summary>
public sealed record ResultadoDeCobro(
    IReadOnlyList<ErrorDePago> Errors,
    IReadOnlyList<PagoValidado> Payments,
    decimal Paid,
    decimal Missing,
    decimal Excess,
    bool IsCredit)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// El validador puro de los pagos de un documento de venta o de una nota (feature 012, I3, T579; FR-056, FR-097, FR-101, T25,
/// T26; contracts/api.md §22.4). Sin IO: la disponibilidad del medio la decide <see cref="DisponibilidadDeMedio"/>, el bono
/// repetido entre cajas lo garantiza el índice de <c>INV_VoucherRedemptions</c> y el crédito provisional su propio paso.
/// <list type="bullet">
/// <item>lo aplicado suma <b>exactamente</b> el total a pagar (<c>Payments.TotalMismatch</c> con lo que falta o sobra);</item>
/// <item>vueltas sólo en medios que las admiten, y entregado − aplicado = vueltas ≥ 0 (<c>Payments.ChangeNotAllowed</c>);</item>
/// <item>referencia obligatoria según el medio y dentro de su longitud; el mismo medio con la misma referencia normalizada es
/// <c>Payments.DuplicateReference</c>;</item>
/// <item><c>last4</c> son exactamente cuatro dígitos; cualquier campo con 13 a 19 dígitos seguidos (con o sin espacios o guiones)
/// es <c>Payments.CardNumberNotAllowed</c>: el número de la tarjeta nunca se guarda;</item>
/// <item>un medio sin pago parcial cubre todo lo que falta cuando se agrega (<c>Payments.PartialNotAllowed</c>);</item>
/// <item>todo medio que se arquea exige una sesión abierta del usuario (<c>Payments.CashSessionRequired</c>);</item>
/// <item>un pago de clase crédito vuelve la venta «a crédito».</item>
/// </list>
/// (nuevo)
/// </summary>
public static partial class ValidadorDePagos
{
    public const string TotalMismatch = "Payments.TotalMismatch";
    public const string ChangeNotAllowed = "Payments.ChangeNotAllowed";
    public const string ReferenceRequired = "Payments.ReferenceRequired";
    public const string ReferenceInvalid = "Payments.ReferenceInvalid";
    public const string DuplicateReference = "Payments.DuplicateReference";
    public const string CardNumberNotAllowed = "Payments.CardNumberNotAllowed";
    public const string PartialNotAllowed = "Payments.PartialNotAllowed";
    public const string CashSessionRequired = "Payments.CashSessionRequired";

    /// <summary>Un pago con valor cero o negativo (nuevo en §2.17).</summary>
    public const string AmountInvalid = "Payments.AmountInvalid";

    /// <summary>13 a 19 dígitos seguidos, admitiendo un espacio o guion entre dígitos, sin más dígitos a los lados.</summary>
    [GeneratedRegex(@"(?<!\d[ -]?)\d(?:[ -]?\d){12,18}(?![ -]?\d)", RegexOptions.CultureInvariant)]
    private static partial Regex NumeroDeTarjeta();

    [GeneratedRegex(@"^\d{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex CuatroDigitos();

    /// <summary>¿El texto contiene algo con forma de número de tarjeta completo?</summary>
    public static bool PareceNumeroDeTarjeta(string? texto) => !string.IsNullOrEmpty(texto) && NumeroDeTarjeta().IsMatch(texto);

    /// <summary>Mayúsculas, sin espacios ni guiones; nulo si queda vacía.</summary>
    public static string? NormalizarReferencia(string? referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia)) return null;
        var limpia = new string(referencia.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();
        return limpia.Length == 0 ? null : limpia;
    }

    public static ResultadoDeCobro Validar(PedidoDeCobro pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var errores = new List<ErrorDePago>();
        var validados = new List<PagoValidado>();
        var vistas = new HashSet<(int Medio, string Referencia)>();
        var aplicadoAntes = 0m;

        for (var i = 0; i < pedido.Pagos.Count; i++)
        {
            var pago = pedido.Pagos[i];
            var medio = pago.Medio;
            var erroresAntes = errores.Count;

            if (pago.Amount <= 0m)
                errores.Add(Error(AmountInvalid, i, ("paymentMeansCode", medio.Code), ("amount", pago.Amount)));

            // Ningún campo lleva el número de la tarjeta (FR-101, LosPagosNoGuardanElNumeroDeTarjeta).
            foreach (var (campo, valor) in new[] { ("reference", pago.Reference), ("authorizationCode", pago.AuthorizationCode),
                         ("last4", pago.Last4), ("batchNumber", pago.TerminalBatchNumber) })
            {
                if (PareceNumeroDeTarjeta(valor))
                    errores.Add(Error(CardNumberNotAllowed, i, ("paymentMeansCode", medio.Code), ("field", campo)));
            }

            if (pago.Last4 is not null && !PareceNumeroDeTarjeta(pago.Last4) && !CuatroDigitos().IsMatch(pago.Last4))
                errores.Add(Error(ReferenceInvalid, i, ("paymentMeansCode", medio.Code), ("field", "last4")));

            var cambio = 0m;
            if (pago.Tendered is { } entregado && entregado != pago.Amount)
            {
                if (!medio.AllowsChange || entregado < pago.Amount)
                    errores.Add(Error(ChangeNotAllowed, i, ("paymentMeansCode", medio.Code), ("tendered", entregado), ("amount", pago.Amount)));
                else
                    cambio = entregado - pago.Amount;
            }

            var normalizada = NormalizarReferencia(pago.Reference);
            if (medio.RequiresReference && normalizada is null)
            {
                errores.Add(Error(ReferenceRequired, i, ("paymentMeansCode", medio.Code), ("referenceKind", medio.ReferenceKind)));
            }
            else if (pago.Reference is not null && !PareceNumeroDeTarjeta(pago.Reference))
            {
                var largo = pago.Reference.Trim().Length;
                if ((medio.ReferenceMinLength is { } min && largo < min) || (medio.ReferenceMaxLength is { } max && largo > max))
                {
                    errores.Add(Error(ReferenceInvalid, i, ("paymentMeansCode", medio.Code), ("field", "reference"),
                        ("minLength", medio.ReferenceMinLength), ("maxLength", medio.ReferenceMaxLength)));
                }
            }

            if (normalizada is not null && !vistas.Add((medio.PaymentMeansId, normalizada)))
                errores.Add(Error(DuplicateReference, i, ("paymentMeansCode", medio.Code), ("reference", normalizada)));

            if (!medio.AllowsPartial && pago.Amount > 0m && pago.Amount != pedido.AmountDue - aplicadoAntes)
            {
                errores.Add(Error(PartialNotAllowed, i, ("paymentMeansCode", medio.Code), ("amount", pago.Amount),
                    ("remaining", pedido.AmountDue - aplicadoAntes)));
            }

            if (medio.CountMethod != CashCountMethod.None
                && (pago.CashSessionId is not { } sesion || !pedido.OpenCashSessionIds.Contains(sesion)))
            {
                errores.Add(Error(CashSessionRequired, i, ("paymentMeansCode", medio.Code)));
            }

            aplicadoAntes += pago.Amount;
            if (errores.Count == erroresAntes) validados.Add(new PagoValidado(i, pago.Amount, pago.Tendered, cambio, normalizada));
        }

        var pagado = pedido.Pagos.Sum(p => p.Amount);
        var falta = Math.Max(0m, pedido.AmountDue - pagado);
        var sobra = Math.Max(0m, pagado - pedido.AmountDue);
        if (pagado != pedido.AmountDue)
            errores.Add(Error(TotalMismatch, null, ("amountDue", pedido.AmountDue), ("paid", pagado), ("missing", falta), ("excess", sobra)));

        return new ResultadoDeCobro(errores, validados, pagado, falta, sobra, pedido.Pagos.Any(p => ClasesDeMedio.EsCredito(p.Medio.Class)));
    }

    private static ErrorDePago Error(string codigo, int? indice, params (string Clave, object? Valor)[] datos) =>
        new(codigo, indice, datos.ToDictionary(d => d.Clave, d => d.Valor, StringComparer.Ordinal));
}
