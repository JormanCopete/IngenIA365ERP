using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US10 (T106; contracts/api.md §11): los DTO de Shared del retiro del asociado. Espejo de los de Application; los enums llegan como número
// (EnumPorNombreONumero) y TextosDeRetiros les da nombre en español. (nuevo)

/// <summary>La aprobación (o el rechazo) del retiro con su punto de acta.</summary>
public sealed record AprobacionDeRetiroDto(Guid SessionItemPublicId, string? SessionNumber, DateTime On, DateOnly? EffectiveDate, string? Reason);

/// <summary>Los totales de la liquidación vigente.</summary>
public sealed record LiquidacionDeRetiroDto(
    int Version,
    DateTime SettledAt,
    decimal Contributions,
    decimal Savings,
    decimal Crossed,
    decimal LossDeduction,
    decimal Refund,
    DateOnly? RefundDueOn,
    bool RefundHeldByInstitutionalMinimum,
    Guid? DocumentPublicId,
    string? DocumentNumber);

/// <summary>Una orden de devolución del retiro.</summary>
public sealed record OrdenDeDevolucionDeRetiroDto(Guid PublicId, string Number, string AccountNumber, decimal Amount, int Status, DateOnly ResultDueOn);

/// <summary>Una línea de la liquidación (+ a favor del asociado, − lo que se descuenta; la devolución, informativa).</summary>
public sealed record LineaDeRetiroDto(
    int Version,
    int LineNumber,
    int Kind,
    Guid? AccountPublicId,
    string? AccountNumber,
    Guid? LoanPublicId,
    string? LoanNumber,
    string? Concept,
    decimal Amount,
    string? AccountOperationNumber,
    string? LoanOperationNumber,
    string? Notes);

/// <summary>El trámite del retiro del asociado.</summary>
public sealed record RetiroDeAsociadoDto(
    Guid PublicId,
    string Number,
    PersonaDeCuentaDto Person,
    Guid ContributionAccountPublicId,
    string ContributionAccountNumber,
    int Cause,
    string? CauseDetail,
    DateOnly FiledOn,
    int Status,
    IReadOnlyList<Guid> SupportAttachmentPublicIds,
    AprobacionDeRetiroDto? Approval,
    DateTime? DesistedAt,
    string? DesistReason,
    LiquidacionDeRetiroDto? Settlement,
    string? ToRedoReason,
    DateTime? PaidAt,
    IReadOnlyList<OrdenDeDevolucionDeRetiroDto> RefundOrders,
    IReadOnlyList<LineaDeRetiroDto> Lines);

/// <summary>El cruce contra un crédito en la vista previa (prelación D14).</summary>
public sealed record CruceDeRetiroDto(Guid LoanPublicId, string Number, int Priority, int DaysPastDue, decimal AnnualRate, decimal Payoff, decimal Amount,
    bool PaysOff);

/// <summary>El mínimo irreducible de la entidad frente a la liquidación (FR-080).</summary>
public sealed record MinimoDeLaEntidadDto(decimal Capital, decimal? Minimum, decimal CapitalAfter, bool WouldBreach);

/// <summary>El descuento por pérdidas de la vista previa.</summary>
public sealed record DescuentoPorPerdidasDto(int Year, decimal Proportion, decimal Amount, Guid SessionItemPublicId);

/// <summary>La vista previa de la liquidación, con la huella que exige liquidar.</summary>
public sealed record VistaPreviaDeRetiroDto(
    DateOnly Date,
    IReadOnlyList<LineaDeRetiroDto> Lines,
    IReadOnlyList<CruceDeRetiroDto> Offsets,
    DescuentoPorPerdidasDto? LossDeduction,
    decimal Contributions,
    decimal Savings,
    decimal Crossed,
    decimal Refund,
    DateOnly RefundDueOn,
    MinimoDeLaEntidadDto EntityMinimum,
    string Sha256,
    IReadOnlyList<string> Warnings);

/// <summary>El cuerpo de radicar un retiro.</summary>
public sealed record RadicarRetiroRequest(Guid PersonPublicId, int Cause, string? CauseDetail, DateOnly? RequestedOn, IReadOnlyList<Guid>? SupportAttachmentPublicIds,
    string? Note);

/// <summary>El cuerpo de aprobar o negar con el punto del acta cerrada (y la fecha del retiro al aprobar).</summary>
public sealed record DecisionDeRetiroRequest(Guid SessionItemPublicId, DateOnly? EffectiveDate, string Reason);

/// <summary>El descuento por pérdidas que dispuso el acta: el punto, el año y la proporción (fracción) sobre los aportes.</summary>
public sealed record DescuentoPorPerdidasRequest(Guid SessionItemPublicId, int Year, decimal Proportion);

/// <summary>El cuerpo de la vista previa.</summary>
public sealed record VistaPreviaDeRetiroRequest(DateOnly? Date, DescuentoPorPerdidasRequest? LossDeduction);

/// <summary>El beneficiario bancario de la orden de devolución (opcional: sin él, la cuenta de depósito del asociado o el medio de Tesorería).</summary>
public sealed record BeneficiarioBancarioRequest(Guid BankPublicId, byte AccountType, string AccountNumber);

/// <summary>La forma de la devolución: <c>DisbursementOrder</c> (en K7) o <c>TransferToAccount</c> (K8).</summary>
public sealed record FormaDeDevolucionRequest(string Form, Guid? TargetAccountPublicId = null, BeneficiarioBancarioRequest? Payee = null);

/// <summary>El cuerpo de liquidar.</summary>
public sealed record LiquidarRetiroRequest(DateOnly? Date, string ExpectedPreviewSha256, DescuentoPorPerdidasRequest? LossDeduction, FormaDeDevolucionRequest Refund,
    string Reason);

/// <summary>El cuerpo de aprobar la devolución pendiente.</summary>
public sealed record AprobarDevolucionDeRetiroRequest(Guid? ApprovalPublicId, BeneficiarioBancarioRequest? Payee, string Reason);

/// <summary>Los nombres en español de los estados, las causas y las clases de línea del retiro.</summary>
public static class TextosDeRetiros
{
    public const int Radicado = 1, Aprobado = 2, Liquidado = 3, Pagado = 4, Desistido = 5, Negado = 6, PorRehacer = 7;

    public static IReadOnlyList<(int Valor, string Nombre)> Causas { get; } =
        [(1, "Retiro voluntario"), (2, "Exclusión"), (3, "Fallecimiento"), (99, "Otra")];

    public static IReadOnlyList<(int Valor, string Nombre)> Estados { get; } =
        [(Radicado, "Radicado"), (Aprobado, "Aprobado"), (Liquidado, "Liquidado"), (Pagado, "Pagado"), (Desistido, "Desistido"), (Negado, "Negado"),
         (PorRehacer, "Por rehacer")];

    public static string Estado(int estado) => Estados.FirstOrDefault(e => e.Valor == estado).Nombre ?? estado.ToString(CultureInfo.InvariantCulture);

    public static string Causa(int causa) => Causas.FirstOrDefault(c => c.Valor == causa).Nombre ?? causa.ToString(CultureInfo.InvariantCulture);

    public static string ClaseDeLinea(int clase) => clase switch
    {
        1 => "Aportes",
        2 => "Ahorros",
        3 => "CDAT",
        4 => "Intereses a la fecha",
        5 => "Retención",
        6 => "GMF",
        7 => "Cruce con crédito",
        8 => "Pérdidas",
        9 => "Devolución",
        _ => clase.ToString(CultureInfo.InvariantCulture),
    };

    public static string EstadoDeOrden(int estado) => estado switch
    {
        1 => "Emitida",
        2 => "Girada",
        3 => "Rechazada",
        4 => "Cheque no cobrado",
        5 => "Anulada",
        _ => estado.ToString(CultureInfo.InvariantCulture),
    };
}
