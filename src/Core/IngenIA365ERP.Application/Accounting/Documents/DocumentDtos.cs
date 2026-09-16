using IngenIA365ERP.Application.Accounting.Posting;

namespace IngenIA365ERP.Application.Accounting.Documents;

// Comprobantes (feature 009, US3; contracts/api.md §6). Los DTOs se espejan en
// Shared/Services/Contabilidad/ContabilidadDtos.cs con los mismos nombres de campo.

public sealed record ComprobanteResumenDto(
    Guid PublicId,
    string VoucherTypeCode,
    long? Number,
    DateOnly Date,
    string Description,
    string Status,
    string Kind,
    string OriginModule,
    decimal TotalDebit,
    decimal TotalCredit,
    string RegisteredBy,
    string? PostedBy,
    DateTime? PostedAt,
    int Lines);

public sealed record LineaDeComprobanteDto(
    int LineNumber,
    Guid AccountPublicId,
    string AccountCode,
    string AccountName,
    Guid BranchPublicId,
    string BranchName,
    Guid? CostCenterPublicId,
    string? CostCenterName,
    Guid? PersonPublicId,
    string? PersonName,
    string? PersonTaxId,
    string? CrossDocumentType,
    string? CrossDocumentNumber,
    decimal Debit,
    decimal Credit,
    string? Detail,
    decimal? TaxBase);

/// <summary>Qué módulo generó el comprobante y a qué documento suyo corresponde; <c>Link</c> es la ruta de la pantalla del módulo, si se conoce.</summary>
public sealed record OrigenDeComprobanteDto(string Module, string ModuleName, string? SourceType, Guid? SourcePublicId, string? Link);

public sealed record ReversionDto(Guid? ReversesPublicId, string? ReversesNumber, Guid? ReversedByPublicId, string? ReversedByNumber, string? Reason);

public sealed record AdjuntoDto(Guid PublicId, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt, string? CreatedBy);

/// <summary>Una infracción por línea con el campo que la pantalla señala; <c>Severity</c> «Aviso» no bloquea (FR-041).</summary>
public sealed record ErrorDeLineaDto(int LineNumber, string Field, string Code, string Message, string Severity)
{
    public static ErrorDeLineaDto De(ErrorDeLinea e) => new(e.LineNumber, e.Field, e.Code, e.Message, e.Severidad.ToString());
}

/// <summary><c>GET /documents/{id}</c>: cabecera, líneas, origen, reversión, soportes y, si es borrador, sus infracciones actuales.</summary>
public sealed record ComprobanteDto(
    Guid PublicId,
    string VoucherTypeCode,
    string VoucherTypeName,
    long? Number,
    DateOnly Date,
    string Description,
    string Status,
    string Kind,
    decimal TotalDebit,
    decimal TotalCredit,
    string RegisteredBy,
    DateTime RegisteredAt,
    string? PostedBy,
    DateTime? PostedAt,
    OrigenDeComprobanteDto Origin,
    ReversionDto Reversal,
    IReadOnlyList<LineaDeComprobanteDto> Lines,
    IReadOnlyList<AdjuntoDto> Attachments,
    IReadOnlyList<ErrorDeLineaDto> Errors)
{
    public string Referencia => Number is { } n ? $"{VoucherTypeCode}-{n}" : "borrador";
}

/// <summary>Una línea tal como la digita la pantalla: cuenta por código, referencias por <c>PublicId</c>.</summary>
public sealed record LineaDeBorradorInput(
    string AccountCode,
    Guid? BranchPublicId,
    Guid? CostCenterPublicId,
    Guid? PersonPublicId,
    string? CrossDocumentType,
    string? CrossDocumentNumber,
    decimal Debit,
    decimal Credit,
    string? Detail,
    decimal? TaxBase);

public sealed record BorradorGuardadoDto(Guid PublicId, IReadOnlyList<ErrorDeLineaDto> Errors);

public sealed record ValidacionDto(IReadOnlyList<ErrorDeLineaDto> Errors, decimal TotalDebit, decimal TotalCredit, decimal Difference);

public sealed record ContabilizadoDto(long Number);

public sealed record ReversadoDto(Guid ReversalPublicId, long Number);

/// <summary>Lo que el PDF necesita además del comprobante: la empresa que lo emite.</summary>
public sealed record EmpresaParaImpresion(string Name, string TaxId);

public sealed record ComprobanteImpreso(byte[] Content, string FileName);

/// <summary>Quien dibuja el PDF vive en la API (QuestPDF sólo se conoce allí), como <c>IPayslipPdfRenderer</c>.</summary>
public interface IVoucherPdfRenderer
{
    byte[] Render(ComprobanteDto comprobante, EmpresaParaImpresion empresa);
}

/// <summary>Ruta de la pantalla del módulo para «Ver en {módulo}»; se amplía módulo a módulo (T083, T128).</summary>
public static class EnlacesDeOrigen
{
    public static string? Ruta(string? sourceType, Guid? sourcePublicId) => sourcePublicId is null ? null : sourceType switch
    {
        "PayrollRun" => $"/nomina/liquidacion?corrida={sourcePublicId}",
        _ => null,
    };
}
