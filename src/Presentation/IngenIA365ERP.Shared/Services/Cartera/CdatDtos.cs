namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US6 (T179; contracts/api.md §5 y §6 de la 014): lo que las pantallas del CDAT y de la tasa negociada mandan y reciben por
// /api/lending/term-deposits, /api/lending/negotiated-rate-requests y /api/lending/lines/{id}/negotiated-rate-rules. Los enums viajan y llegan como número
// (los textos están en TextosDeCdat); las tasas y los márgenes viajan como fracción (0,10 = 10 % EA) y la pantalla los digita en puntos. (nuevo)

// --------------------------------------------------------------------------------------------------------------- pedidos --

/// <summary>El destino de los intereses y del vencimiento: una cuenta a la vista del titular (1) o una orden de giro (2).</summary>
public sealed class DestinoDeCdatRequest
{
    public int Kind { get; set; } = TextosDeCdat.DestinoCuenta;
    public Guid? AccountPublicId { get; set; }
    public Guid? PayeePersonPublicId { get; set; }
    public Guid? BankPublicId { get; set; }
    public byte? AccountType { get; set; }
    public string? AccountNumber { get; set; }

    /// <summary>Revisión a mano del 2026-10-27 (E250 de la 014): la instrucción escrita del titular cuando los intereses van a otra persona.</summary>
    public Guid? ThirdPartyAuthorizationAttachmentPublicId { get; set; }
}

/// <summary>El fondeo: por el recaudador (1, consigna el monto exacto) o por traslado desde una cuenta de ahorro del titular (2).</summary>
public sealed class FondeoDeCdatRequest
{
    public int Kind { get; set; } = TextosDeCdat.FondeoRecaudador;
    public Guid? SourceAccountPublicId { get; set; }
    public List<Guid> Signers { get; set; } = [];
    public Guid? ApprovalPublicId { get; set; }
}

/// <summary>El margen pedido sobre la tabla (fracción, puntos EA) y por qué.</summary>
public sealed record NegociacionDeCdatRequest(decimal RequestedMarginEa, string Reason);

/// <summary>El cuerpo de <c>POST /api/lending/term-deposits</c>.</summary>
public sealed class ConstituirCdatRequest
{
    public Guid LinePublicId { get; set; }
    public Guid HolderPersonPublicId { get; set; }
    public Guid OfficePublicId { get; set; }
    public int HandlingMode { get; set; } = TextosDeCaptaciones.Indistinto;
    public List<ParteDeCuentaRequest> Parties { get; set; } = [];
    public List<BeneficiarioDeCuentaRequest> Beneficiaries { get; set; } = [];
    public Guid? BeneficiaryRecordAttachmentPublicId { get; set; }
    public decimal Amount { get; set; }
    public int TermDays { get; set; } = 180;
    public int InterestPeriodicity { get; set; } = TextosDeCdat.AlVencimiento;
    public int Timing { get; set; } = TextosDeCdat.Vencidos;
    public DateOnly? StartDate { get; set; }
    public DestinoDeCdatRequest PaymentDestination { get; set; } = new();
    public int MaturityInstruction { get; set; } = TextosDeCdat.Prorrogar;
    public FondeoDeCdatRequest Funding { get; set; } = new();
    public NegociacionDeCdatRequest? Negotiation { get; set; }
    public string? Channel { get; set; }
    public string? ExpectedPreviewSha256 { get; set; }
}

/// <summary>El cuerpo de <c>POST /api/lending/term-deposits/simulate</c> (sin clave).</summary>
public sealed record SimularCdatRequest(Guid LinePublicId, decimal Amount, int TermDays, int InterestPeriodicity, int Timing, DateOnly? StartDate,
    decimal? RequestedMarginEa, Guid? HolderPersonPublicId, Guid? OfficePublicId, string? Channel);

/// <summary>El fondeo por traslado de un CDAT que espera (<c>POST /term-deposits/{id}/fund</c>).</summary>
public sealed record FondearCdatRequest(Guid SourceAccountPublicId, IReadOnlyList<Guid>? Signers, Guid? ApprovalPublicId, string? Reason);

/// <summary>La instrucción al vencimiento hasta el fin de la gracia (<c>PUT /term-deposits/{id}/maturity-instruction</c>).</summary>
public sealed record InstruccionDeCdatRequest(int Instruction, DestinoDeCdatRequest? PaymentDestination, string? Reason);

/// <summary>El cambio de monto de una renovación: aumento desde una cuenta del titular o disminución a cuenta o giro.</summary>
public sealed record CambioDeMontoDeCdatRequest(string Kind, decimal Amount, Guid? SourceAccountPublicId = null, IReadOnlyList<Guid>? Signers = null,
    DestinoDeCdatRequest? Destination = null);

/// <summary>El cuerpo de <c>POST /term-deposits/{id}/renew</c>.</summary>
public sealed record RenovarCdatRequest(int TermDays, int InterestPeriodicity, int Timing, string Reason, CambioDeMontoDeCdatRequest? AmountChange = null,
    decimal? RequestedMarginEa = null, int? MaturityInstruction = null, DestinoDeCdatRequest? PaymentDestination = null);

/// <summary>La fecha de la vista previa de la redención (<c>POST /term-deposits/{id}/redeem/simulate</c>).</summary>
public sealed record SimularRedencionRequest(DateOnly Date);

/// <summary>El destino de la redención: a una cuenta del titular (1) o por giro (2).</summary>
public sealed record DestinoDeRedencionRequest(int Kind, Guid? TargetAccountPublicId = null, Guid? PayeePersonPublicId = null, Guid? BankPublicId = null,
    byte? AccountType = null, string? AccountNumber = null);

/// <summary>El cuerpo de <c>POST /term-deposits/{id}/redeem</c>, con la huella de la vista previa.</summary>
public sealed record RedimirCdatRequest(DestinoDeRedencionRequest Destination, string? ExpectedPreviewSha256, string? Reason, Guid? ApprovalPublicId = null);

/// <summary>El cuerpo de aprobar o negar una solicitud de tasa negociada.</summary>
public sealed record DecidirTasaNegociadaRequest(string? Reason, Guid? ApprovalPublicId = null, Guid? SessionItemPublicId = null);

/// <summary>Los criterios de una regla de tasa negociada (nulo = no restringe).</summary>
public sealed class CriteriosDeTasaNegociada
{
    public int? MinSeniorityMonths { get; set; }
    public decimal? AmountFrom { get; set; }
    public decimal? AmountTo { get; set; }
    public int? TermDaysFrom { get; set; }
    public int? TermDaysTo { get; set; }
    public decimal? BaseRateFrom { get; set; }
    public decimal? BaseRateTo { get; set; }
    public decimal? HolderBalanceFrom { get; set; }
    public decimal? HolderBalanceTo { get; set; }
    public Guid? OfficePublicId { get; set; }
    public string? Channel { get; set; }
}

/// <summary>El alta o la versión nueva de una regla de tasa negociada.</summary>
public sealed class ReglaDeTasaNegociadaRequest
{
    public string Code { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
    public CriteriosDeTasaNegociada Criteria { get; set; } = new();
    public decimal MaxMarginEa { get; set; }
    public bool? KeepsMarginOnRollover { get; set; }
    public DateOnly ValidFrom { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Reason { get; set; } = string.Empty;
}

/// <summary>El fin de una regla de tasa negociada.</summary>
public sealed record TerminarReglaDeTasaRequest(DateOnly ValidTo, string Reason);

// ------------------------------------------------------------------------------------------------------------ respuestas --

public sealed record LineaDelPlanDeCdatDto(int Number, DateOnly From, DateOnly To, DateOnly PaymentDate, int Days, decimal Rate, decimal Interest,
    decimal WithholdingEstimate, decimal Net, DateOnly? PaidOn, Guid? OperationPublicId);

public sealed record RetencionEstimadaDeCdatDto(bool AppliesThreshold, decimal Amount);

public sealed record NegociacionDeCdatDto(bool Allowed, Guid? RulePublicId, string? RuleCode, decimal? MaxMarginEa, bool WithinUserAttribution, string? Code,
    string Explanation, string? LevelCode);

public sealed record SimulacionDeCdatDto(decimal TableRate, decimal Rate, IReadOnlyList<LineaDelPlanDeCdatDto> Schedule, decimal TotalInterest,
    RetencionEstimadaDeCdatDto WithholdingEstimate, DateOnly StartDate, DateOnly MaturityDate, DateOnly PaymentDate, int IssueTermBand,
    NegociacionDeCdatDto? Negotiation, string Fingerprint);

public sealed record NegociadoDeCdatDto(decimal MarginEa, Guid? RulePublicId, string? ApprovedBy, string? Reason);

public sealed record CondicionesDeCdatDto(int Version, string TitleNumber, int Cause, decimal Amount, int TermDays, DateOnly StartDate, DateOnly MaturityDate,
    DateOnly PaymentDate, decimal EffectiveAnnual, decimal TableRate, NegociadoDeCdatDto? Negotiated, int InterestPeriodicity, int Timing,
    int? PaymentDestination, int? MaturityInstruction, DateOnly? GraceUntil, int? IssueTermBand, int RolloverCount, string RulesVersion);

public sealed record PropuestaDeCdatDto(decimal Amount, int TermDays, int InterestPeriodicity, int Timing, decimal TableRate, decimal? MarginEa, decimal Rate,
    int Funding, int MaturityInstruction, DateOnly RequestedStart);

public sealed record TituloDeCdatDto(Guid DocumentPublicId, string Number, int? TermsVersion, string Status, DateTime IssuedAt);

public sealed record SolicitudDeTasaNegociadaDto(Guid PublicId, Guid AccountPublicId, string AccountNumber, PersonaDeCuentaDto Holder, decimal Amount, int? TermDays,
    decimal TableRate, decimal RequestedMarginEa, decimal MaxMarginEa, string Rule, string RequestedBy, int Status, string? LevelCode, string? RoutingKind,
    string Reason, DateTime RequestedAt, string? DecidedBy, DateTime? DecidedAt, string? DecisionReason, decimal CostVsTable);

public sealed record CdatDto(CuentaDto Account, CondicionesDeCdatDto? CurrentTerms, PropuestaDeCdatDto? Proposal, IReadOnlyList<CondicionesDeCdatDto> TermsHistory,
    IReadOnlyList<TituloDeCdatDto> Titles, int Status, SolicitudDeTasaNegociadaDto? PendingNegotiation, int? EffectiveInstruction);

public sealed record CdatEnListaDto(Guid PublicId, string Number, string? TitleNumber, PersonaDeCuentaDto Holder, LineaDeCuentaDto Line, decimal Amount, int? TermDays,
    decimal? EffectiveAnnual, decimal? NegotiatedMargin, int? IssueTermBand, DateOnly OpenedOn, DateOnly? MaturityDate, int Status, int? Instruction,
    int RolloverCount);

public sealed record VencimientoDelDiaDto(DateOnly Date, int IssueTermBand, int Count, decimal Principal);

public sealed record PenalidadDeCdatDto(string? Kind, decimal? RepricedRate, decimal? RepricedInterest, decimal Amount);

public sealed record VistaPreviaDeRedencionDto(DateOnly Date, bool IsEarly, decimal Principal, decimal AccruedInterest, PenalidadDeCdatDto Penalty, decimal Interest,
    decimal Withholding, decimal GmfIfCreditedToAccount, decimal Net, IReadOnlyList<string> Explanation, string Fingerprint);

public sealed record RedencionDeCdatDto(string Status, Guid? OperationPublicId, string? OperationNumber, Guid? PayoutOrderPublicId, decimal Delivered,
    Guid? ApprovalPublicId, CdatDto? TermDeposit)
{
    public bool EsperaAprobacion => Status == "PendingApproval";
}

public sealed record CriteriosDeTasaNegociadaDto(int? MinSeniorityMonths, decimal? AmountFrom, decimal? AmountTo, int? TermDaysFrom, int? TermDaysTo,
    decimal? BaseRateFrom, decimal? BaseRateTo, decimal? HolderBalanceFrom, decimal? HolderBalanceTo, Guid? OfficePublicId, string? Channel);

public sealed record ReglaDeTasaNegociadaDto(Guid PublicId, string Code, int Version, int Priority, CriteriosDeTasaNegociadaDto Criteria, decimal MaxMarginEa,
    bool? KeepsMarginOnRollover, DateOnly ValidFrom, bool IsActive, string Reason);

/// <summary>Los textos y valores del CDAT para las pantallas (los valores son los de los enums del dominio). (nuevo)</summary>
public static class TextosDeCdat
{
    public const int DestinoCuenta = 1;
    public const int DestinoGiro = 2;
    public const int FondeoRecaudador = 1;
    public const int FondeoTraslado = 2;
    public const int AlVencimiento = 0;
    public const int Vencidos = 1;
    public const int Anticipados = 2;
    public const int Prorrogar = 1;
    public const int Renovar = 2;
    public const int CancelarYPagar = 3;
    public const int SolicitudEnEspera = 1;
    public const string Aumento = "Increase";
    public const string Disminucion = "Decrease";

    /// <summary>Las periodicidades de intereses de un CDAT (<c>RepaymentPeriodicity</c>).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> Periodicidades { get; } =
        [(AlVencimiento, "Al vencimiento"), (30, "Mensual"), (60, "Bimestral"), (90, "Trimestral"), (180, "Semestral"), (360, "Anual")];

    public static IReadOnlyList<(int Valor, string Texto)> Modalidades { get; } = [(Vencidos, "Vencidos"), (Anticipados, "Anticipados")];

    public static IReadOnlyList<(int Valor, string Texto)> Instrucciones { get; } =
        [(Prorrogar, "Prorrogar"), (Renovar, "Renovar con condiciones nuevas"), (CancelarYPagar, "Cancelar y pagar")];

    public static IReadOnlyList<(int Valor, string Texto)> PlazosDeEmision { get; } =
        [(1, "Menos de 6 meses"), (2, "De 6 a 12 meses"), (3, "De 12 a 18 meses"), (4, "18 meses o más")];

    public static IReadOnlyList<(int Valor, string Texto)> Causas { get; } =
        [(1, "Constitución"), (2, "Prórroga"), (3, "Renovación"), (4, "Migración"), (5, "Contractual"), (6, "Incumplimiento"), (7, "Tasa pactada"),
            (8, "Tasa negociada")];

    public static IReadOnlyList<(int Valor, string Texto)> EstadosDeSolicitud { get; } =
        [(SolicitudEnEspera, "En espera de decisión"), (2, "Aprobada"), (3, "Negada"), (4, "Retirada"), (5, "Vencida")];

    public static IReadOnlyList<(int Valor, string Texto)> Destinos { get; } = [(DestinoCuenta, "Abono en una cuenta a la vista"), (DestinoGiro, "Orden de giro")];

    public static string Periodicidad(int v) => De(Periodicidades, v);
    public static string Modalidad(int v) => De(Modalidades, v);
    public static string Instruccion(int? v) => v is { } x ? De(Instrucciones, x) : "—";
    public static string PlazoDeEmision(int? v) => v is { } x ? De(PlazosDeEmision, x) : "—";
    public static string Causa(int v) => De(Causas, v);
    public static string EstadoDeSolicitud(int v) => De(EstadosDeSolicitud, v);
    public static string Destino(int? v) => v is { } x ? De(Destinos, x) : "—";

    /// <summary>Una tasa en fracción, en puntos con cuatro decimales (0,102 = 10,2000 %).</summary>
    public static string Puntos(decimal? fraccion) => fraccion is { } f ? (f * 100m).ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("es-CO")) + " %" : "—";

    private static string De(IReadOnlyList<(int Valor, string Texto)> lista, int v) =>
        lista.FirstOrDefault(x => x.Valor == v).Texto ?? v.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
