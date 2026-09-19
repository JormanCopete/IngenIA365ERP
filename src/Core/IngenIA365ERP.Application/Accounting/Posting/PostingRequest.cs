using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Application.Accounting.Posting;

/// <summary>Quién manda el comprobante y a qué documento suyo corresponde (contracts/contabilizacion.md §1).</summary>
public sealed record AccountingOrigin(string Module, string SourceType, Guid SourcePublicId)
{
    public bool EsContabilidad => Module == ModuloContable.Contabilidad;

    /// <summary>La digitación manual: el comprobante es su propio origen.</summary>
    public static AccountingOrigin Manual(Guid documentPublicId) =>
        new(ModuloContable.Contabilidad, nameof(AccountingDocument), documentPublicId);
}

/// <summary>
/// Una línea que un módulo o la digitación entrega al contrato. La cuenta se referencia por
/// código o por Id (uno de los dos); la sucursal en null se resuelve a la propuesta (la principal,
/// o la del usuario al digitar); el centro de costo lo conserva la cuenta sólo si lo maneja.
/// </summary>
public sealed record PostingLine
{
    public string? AccountCode { get; init; }
    public int? AccountId { get; init; }
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public string? Detail { get; init; }
    public int? PersonId { get; init; }
    public int? BranchId { get; init; }
    public int? CostCenterId { get; init; }
    public string? CrossDocumentType { get; init; }
    public string? CrossDocumentNumber { get; init; }
    public decimal? TaxBase { get; init; }

    /// <summary>Cómo se nombra la cuenta en los errores: el código si vino, si no el Id.</summary>
    public string AccountRef => !string.IsNullOrWhiteSpace(AccountCode) ? AccountCode.Trim() : AccountId?.ToString() ?? string.Empty;

    public decimal Importe => Debit > 0m ? Debit : Credit;

    public static PostingLine Debito(int accountId, decimal valor, string? detalle = null) =>
        new() { AccountId = accountId, Debit = valor, Detail = detalle };

    public static PostingLine Credito(int accountId, decimal valor, string? detalle = null) =>
        new() { AccountId = accountId, Credit = valor, Detail = detalle };

    public static PostingLine Debito(string accountCode, decimal valor, string? detalle = null) =>
        new() { AccountCode = accountCode, Debit = valor, Detail = detalle };

    public static PostingLine Credito(string accountCode, decimal valor, string? detalle = null) =>
        new() { AccountCode = accountCode, Credit = valor, Detail = detalle };
}

/// <summary>Lo que se pide contabilizar: tipo, fecha, descripción, origen, líneas y clase de documento.</summary>
public sealed record PostingRequest(
    string VoucherTypeCode,
    DateOnly Date,
    string Description,
    AccountingOrigin Origin,
    IReadOnlyList<PostingLine> Lines,
    DocumentKind Kind = DocumentKind.Regular);

/// <summary>Resultado de validar sin contabilizar (<c>POST /documents/validate</c>): errores, avisos y totales.</summary>
public sealed record ValidacionDeComprobante(
    IReadOnlyList<ErrorDeLinea> Errores,
    IReadOnlyList<ErrorDeLinea> Avisos,
    decimal TotalDebit,
    decimal TotalCredit)
{
    public bool EsValido => Errores.Count == 0;
    public decimal Diferencia => TotalDebit - TotalCredit;
}

public static class ComprobanteExtensiones
{
    /// <summary>«NM-12»: tipo y consecutivo; «borrador» mientras no tiene número.</summary>
    public static string Referencia(this AccountingDocument documento) =>
        documento.Number is { } numero ? $"{documento.VoucherType?.Code ?? "?"}-{numero}" : "borrador";
}
