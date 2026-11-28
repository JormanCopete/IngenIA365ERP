using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Payoffs;

/// <summary>
/// El producto que recoge un crédito nuevo, como lo guardan las recogidas proyectadas y aplicadas (feature 028, data-model): su clase y el único
/// identificador que le corresponde —el crédito (<c>LND_Loans</c>), la cuenta (<c>LND_MemberAccounts</c>) o el documento cruce de la factura de
/// Comercial, que es saldo de Contabilidad y Cartera sólo guarda como dato— y el número visible, copiado para el informe. Se construye por sus tres
/// fábricas: así nunca llega a la base una fila con dos identificadores o con el de otra clase. (nuevo)
/// </summary>
public sealed record ProductoRecogido
{
    public const int LargoDelNumero = 40;
    public const int LargoDeLaReferencia = 60;

    private ProductoRecogido(PayoffProductKind clase, int? creditoId, int? cuentaId, string? referencia, string numero)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numero);
        Clase = clase;
        LoanId = creditoId;
        AccountId = cuentaId;
        CommercialReceivableReference = referencia;
        Numero = Recortar(numero.Trim(), LargoDelNumero);
    }

    public PayoffProductKind Clase { get; }

    public int? LoanId { get; }

    public int? AccountId { get; }

    public string? CommercialReceivableReference { get; }

    /// <summary>El número que ve la persona: el del crédito, el de la cuenta o el de la factura.</summary>
    public string Numero { get; }

    public static ProductoRecogido Credito(int creditoId, string numero) =>
        creditoId > 0 ? new(PayoffProductKind.Loan, creditoId, null, null, numero) : throw new ArgumentOutOfRangeException(nameof(creditoId));

    public static ProductoRecogido Cuenta(int cuentaId, string numero) =>
        cuentaId > 0 ? new(PayoffProductKind.Account, null, cuentaId, null, numero) : throw new ArgumentOutOfRangeException(nameof(cuentaId));

    public static ProductoRecogido Factura(string referencia, string numero)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referencia);
        var r = referencia.Trim();
        if (r.Length > LargoDeLaReferencia)
            throw new ArgumentOutOfRangeException(nameof(referencia), $"El documento cruce de la factura pasa de {LargoDeLaReferencia} caracteres.");
        return new(PayoffProductKind.CommercialReceivable, null, null, r, numero);
    }

    private static string Recortar(string s, int largo) => s.Length <= largo ? s : s[..largo];
}
