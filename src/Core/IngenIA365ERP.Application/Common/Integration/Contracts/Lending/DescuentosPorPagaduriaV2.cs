using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>DescuentosPorPagaduria</c> v2 (feature 014, T091; decisión del dueño del 2026-10-18, D36; mensajes.md §5.4 de la 014; <c>CAR → Receipts</c>): el
/// archivo de un <b>grupo</b> del convenio, con líneas de créditos y de cuotas de cuentas, cada una con el código de la pagaduría que la agrupa, y los
/// totales por código (lo que la pagaduría verá). Como la v1, sin bytes. Clave <c>Confirmation</c>. Los convenios sin grupos siguen en v1. (nuevo)
/// </summary>
public sealed record DescuentosPorPagaduriaV2
{
    public const string Type = DescuentosPorPagaduriaV1.Type;

    public PayrollAgreementRefV1 Agreement { get; init; } = new();

    public DeductionFileGroupRefV2 FileGroup { get; init; } = new();

    public PayrollPeriodRefV1 Period { get; init; } = new();

    public int FileVersion { get; init; }

    /// <summary>El archivo de ida del grupo (<c>BankFileScope.PayrollDeductionRequest</c>).</summary>
    public DeductionFileRefV1 File { get; init; } = new();

    public DateOnly SendBy { get; init; }

    public IReadOnlyList<DeductionFileLineV2> Lines { get; init; } = [];

    /// <summary>Lo que la pagaduría verá por cada código suyo.</summary>
    public IReadOnlyList<DeductionConceptTotalV2> ConceptTotals { get; init; } = [];

    public DeductionFileTotalsV1 Totals { get; init; } = new();

    public SupersededDeductionFileV1? SupersedesFile { get; init; }

    public UserRefV1 GeneratedBy { get; init; } = new();
}

/// <summary>El grupo de archivo del convenio (<c>PayrollAgreementFileGroup</c>; mensajes.md §5.4, <c>fileGroup</c>).</summary>
public sealed record DeductionFileGroupRefV2
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>El número de la versión del convenio que define el grupo.</summary>
    public int Version { get; init; }
}

/// <summary>Una línea del archivo de un grupo: un crédito o una cuota de cuenta (mensajes.md §5.4, <c>lines</c>).</summary>
public sealed record DeductionFileLineV2
{
    /// <summary>Línea del crédito (<c>LND_PayrollDeductionFileLines</c>) o de la cuota (<c>LND_PayrollDeductionFileAccountLines</c>).</summary>
    public Guid DeductionFileLinePublicId { get; init; }

    /// <summary><c>Loan</c> o <c>AccountInstallment</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public LoanRefV1? Loan { get; init; }

    public MemberAccountRefV1? Account { get; init; }

    public Guid? InstallmentPublicId { get; init; }

    public int? InstallmentNumber { get; init; }

    public Guid PersonPublicId { get; init; }

    public string PayerEmployeeId { get; init; } = string.Empty;

    public string PaymasterConceptCode { get; init; } = string.Empty;

    public DateOnly DueDate { get; init; }

    /// <summary>Valor de <c>PayrollNoveltyKind</c>.</summary>
    public string NoveltyKind { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public decimal PastDueAmount { get; init; }
}

/// <summary>Los totales de un código de la pagaduría (mensajes.md §5.4, <c>conceptTotals</c>).</summary>
public sealed record DeductionConceptTotalV2
{
    public string PaymasterConceptCode { get; init; } = string.Empty;

    public int Lines { get; init; }

    public decimal Amount { get; init; }
}
