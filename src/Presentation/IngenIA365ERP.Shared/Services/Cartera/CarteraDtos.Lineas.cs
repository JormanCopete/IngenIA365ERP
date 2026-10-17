namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US1 (T0359; contracts/api.md §3.1–§3.3): los espejos de las líneas de crédito, sus versiones, la consulta de usura, el
// puntaje, las modalidades del catálogo nacional y las equivalencias de Comercio, tal como los publica Endpoints/Lending/CreditLinesEndpoints.cs.
// Los enums SALEN de la API como número y así se leen y se mandan aquí (la API los acepta por nombre o por número); las pantallas los
// muestran con TextosDeCartera. Las tasas son fracción (0,245 = 24,5 % EA), los montos en pesos y los plazos en meses. Lo que se manda
// es una clase mutable porque el editor de la versión se enlaza campo a campo. (nuevo)

// ------------------------------------------------------------------------------------------------------ entrada --

/// <summary>La tasa corriente: clase (1 fija, 2 variable) y, si es fija, su efectiva anual.</summary>
public sealed class TasaCorrienteRequest
{
    public int Kind { get; set; } = 1;
    public decimal? EffectiveAnnual { get; set; }
}

/// <summary>La tasa variable (D10): índice nacional y su clave, margen en la expresión del índice, piso, techo y reajuste.</summary>
public sealed class TasaVariableRequest
{
    public string IndexCode { get; set; } = "IBR";
    public string IndexKey { get; set; } = string.Empty;
    public decimal Margin { get; set; }
    public decimal? Floor { get; set; }
    public decimal? Ceiling { get; set; }
    public int? ResetPeriodicity { get; set; }
}

/// <summary>La mora: 1 fija (EA) o 2 relativa a la corriente (factor; pendiente de validar por la contadora).</summary>
public sealed class TasaDeMoraRequest
{
    public int Kind { get; set; } = 2;
    public decimal? EffectiveAnnual { get; set; }
    public decimal? Factor { get; set; } = 1m;
}

/// <summary>Una banda de tasa por monto, plazo en meses y segmento del deudor.</summary>
public sealed class BandaDeTasaRequest
{
    public decimal? AmountFrom { get; set; }
    public decimal? AmountTo { get; set; }
    public int? TermFrom { get; set; }
    public int? TermTo { get; set; }
    public int? DebtorKind { get; set; }
    public bool? ForEmployees { get; set; }
    public int? MinSeniorityMonths { get; set; }
    public decimal? EffectiveAnnual { get; set; }
    public decimal? Margin { get; set; }
}

public sealed class TasasDeLineaRequest
{
    public TasaCorrienteRequest Current { get; set; } = new();
    public TasaVariableRequest? Variable { get; set; }
    public TasaDeMoraRequest Default { get; set; } = new();
    public List<BandaDeTasaRequest> Bands { get; set; } = [];
}

public sealed class RangoDeMontosRequest
{
    public decimal Min { get; set; }
    public decimal Max { get; set; }
}

/// <summary>Plazos en meses y el máximo de cuotas que informa la consulta de cupo.</summary>
public sealed class RangoDePlazosRequest
{
    public int MinMonths { get; set; }
    public int MaxMonths { get; set; }
    public int? MaxInstallments { get; set; }
}

public sealed class GraciaRequest
{
    public int Kind { get; set; }
    public int? MaxInstallments { get; set; }
    public int? InterestTreatment { get; set; }
    public int? DistributionInstallments { get; set; }
}

public sealed class AmortizacionRequest
{
    public List<int> Systems { get; set; } = [1];
    public int? Default { get; set; }
    public bool AllowsExtraordinary { get; set; }
    public decimal? MaxExtraordinaryFraction { get; set; }
    public GraciaRequest Grace { get; set; } = new();
    public bool AllowsTranches { get; set; }
}

/// <summary>El redondeo: unidad (0,01; 1; 10; 100; 1.000), modo y momento.</summary>
public sealed class RedondeoRequest
{
    public decimal? Unit { get; set; }
    public int Mode { get; set; } = 1;
    public int Moment { get; set; } = 2;
}

public sealed class CupoYAportesRequest
{
    public decimal? ContributionsMultiple { get; set; }
    public decimal? MaxIndebtedness { get; set; }
    public decimal? MinContributions { get; set; }
}

public sealed class RequisitosRequest
{
    public int? MinAssociateSeniorityMonths { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public bool RequiresEmployment { get; set; }
}

/// <summary>El concepto tributario del interés (D20): gravado o no, con el adjunto del concepto de la contadora.</summary>
public sealed class ConceptoTributarioRequest
{
    public bool Taxed { get; set; }
    public Guid? SupportAttachmentPublicId { get; set; }
}

public sealed class DocumentoExigidoRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Mandatory { get; set; } = true;
    public int? AppliesTo { get; set; }
    public decimal? AmountFrom { get; set; }
    public decimal? AmountTo { get; set; }
    public int? DebtorKind { get; set; }
    public int? PaymentForm { get; set; }
    public int? MaxAgeDays { get; set; }
}

/// <summary>Un seguro: la aseguradora es persona del maestro; si la cooperativa retiene parte de la prima, computa para usura.</summary>
public sealed class SeguroRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid InsurerPersonPublicId { get; set; }
    public int Base { get; set; } = 1;
    public decimal Rate { get; set; }
    public int Periodicity { get; set; } = 30;
    public bool Mandatory { get; set; } = true;
    public decimal? RetainedFraction { get; set; }
    public bool SelfInsured { get; set; }
    public bool IsDebtorLifeInsurance { get; set; }
    public int AccrualTiming { get; set; } = 1;
}

public sealed class ReglaDeGarantiaRequest
{
    public decimal? AmountFrom { get; set; }
    public decimal? AmountTo { get; set; }
    public int? TermFrom { get; set; }
    public int? TermTo { get; set; }
    public int CoDebtors { get; set; }
    public List<int> AdmittedKinds { get; set; } = [];
    public decimal? MinCoverage { get; set; }
    public bool ContributionsCount { get; set; }
    public int? PromissoryNoteKind { get; set; }
}

/// <summary>
/// Lo que sella cada versión (<c>CreditLineVersionInput</c> de api.md §3.1). El puntaje no va aquí: lo cambia su propia ruta, y una versión
/// nueva conserva el de la vigente.
/// </summary>
public sealed class VersionDeLineaRequest
{
    public DateOnly ValidFrom { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ModalityCode { get; set; } = string.Empty;
    public string? SubsegmentCode { get; set; }
    public bool ForEmployees { get; set; }
    public bool AllowsNonAssociates { get; set; }
    public bool AllowsCommercialOrigin { get; set; }
    public int? CommercialDefaultFor { get; set; }
    public TasasDeLineaRequest Rates { get; set; } = new();
    public RangoDeMontosRequest Amounts { get; set; } = new();
    public RangoDePlazosRequest Terms { get; set; } = new();
    public List<int> Periodicities { get; set; } = [30];
    public int? DefaultPeriodicity { get; set; }
    public byte? PaymentDay { get; set; }
    public byte? CutoffDay { get; set; }
    public AmortizacionRequest Amortization { get; set; } = new();
    public int DayCount { get; set; } = 2;
    public int RateEquivalence { get; set; } = 1;
    public RedondeoRequest Rounding { get; set; } = new();

    /// <summary>K6b: la unidad del capital, 1 pesos (defecto) o 2 UVR (sólo en una combinación de vivienda que la admite).</summary>
    public int DenominationUnit { get; set; } = 1;

    public int AllocationOrder { get; set; } = 1;
    public List<int>? ConceptOrder { get; set; }
    public bool SuspenseWithItsConcept { get; set; } = true;
    public bool AllowsSpecificInstallmentOutOfOrder { get; set; }
    public int CommercialNoteRecalculation { get; set; } = 2;
    public int AdvanceOptionFirst { get; set; } = 5;
    public List<int> PaymentForms { get; set; } = [1];
    public int? DefaultPaymentForm { get; set; }
    public RequisitosRequest Requirements { get; set; } = new();
    public CupoYAportesRequest CreditLimit { get; set; } = new();
    public bool RequiresBureauQuery { get; set; }
    public ConceptoTributarioRequest? InterestTaxConcept { get; set; }
    public List<DocumentoExigidoRequest> Documents { get; set; } = [];
    public List<SeguroRequest> Insurances { get; set; } = [];
    public List<ReglaDeGarantiaRequest> CollateralRules { get; set; } = [];

    /// <summary>
    /// Una versión nueva que parte de <paramref name="v"/> (la vigente): copia todo y deja la fecha y el motivo para que los diga quien
    /// registra. Es lo que el editor muestra al abrir «Nueva versión».
    /// </summary>
    public static VersionDeLineaRequest DesdeLaVigente(VersionDeLineaDto v, DateOnly desde) => new()
    {
        ValidFrom = desde,
        Reason = string.Empty,
        ModalityCode = v.ModalityCode,
        SubsegmentCode = v.SubsegmentCode,
        ForEmployees = v.ForEmployees,
        AllowsNonAssociates = v.AllowsNonAssociates,
        AllowsCommercialOrigin = v.AllowsCommercialOrigin,
        CommercialDefaultFor = v.CommercialDefaultFor,
        Rates = new TasasDeLineaRequest
        {
            Current = new TasaCorrienteRequest { Kind = v.RateKind, EffectiveAnnual = v.EffectiveAnnual },
            Variable = v.RateKind == 2
                ? new TasaVariableRequest
                {
                    IndexCode = v.IndexCode ?? "IBR", IndexKey = v.IndexKey ?? string.Empty, Margin = v.Margin ?? 0m, Floor = v.RateFloor,
                    Ceiling = v.RateCeiling, ResetPeriodicity = v.ResetPeriodicity,
                }
                : null,
            Default = new TasaDeMoraRequest { Kind = v.DefaultRateKind, EffectiveAnnual = v.DefaultRate, Factor = v.DefaultRateFactor },
            Bands = [.. v.RateBands.Select(b => new BandaDeTasaRequest
            {
                AmountFrom = b.AmountFrom, AmountTo = b.AmountTo, TermFrom = b.TermFrom, TermTo = b.TermTo, DebtorKind = b.DebtorKind,
                ForEmployees = b.ForEmployees, MinSeniorityMonths = b.MinSeniorityMonths, EffectiveAnnual = b.EffectiveAnnual, Margin = b.Margin,
            })],
        },
        Amounts = new RangoDeMontosRequest { Min = v.MinAmount, Max = v.MaxAmount },
        Terms = new RangoDePlazosRequest { MinMonths = v.MinTermMonths, MaxMonths = v.MaxTermMonths, MaxInstallments = v.MaxInstallments },
        Periodicities = [.. v.Periodicities],
        DefaultPeriodicity = v.DefaultPeriodicity,
        PaymentDay = v.PaymentDay,
        CutoffDay = v.CutoffDay,
        Amortization = new AmortizacionRequest
        {
            Systems = [.. v.AmortizationSystems], Default = v.DefaultAmortizationSystem, AllowsExtraordinary = v.AllowsExtraordinaryInstallments,
            MaxExtraordinaryFraction = v.MaxExtraordinaryFraction, AllowsTranches = v.AllowsTranches,
            Grace = new GraciaRequest
            {
                Kind = v.AllowedGraceKind, MaxInstallments = v.MaxGraceInstallments, InterestTreatment = v.GraceInterestTreatment,
                DistributionInstallments = v.GraceDistributionInstallments,
            },
        },
        DayCount = v.DayCount,
        RateEquivalence = v.RateEquivalence,
        Rounding = new RedondeoRequest { Unit = v.RoundingUnit, Mode = v.RoundingMode, Moment = v.RoundingMoment },
        DenominationUnit = v.DenominationUnit,
        AllocationOrder = v.AllocationOrder,
        ConceptOrder = [.. v.ConceptOrder],
        SuspenseWithItsConcept = v.SuspenseWithItsConcept,
        AllowsSpecificInstallmentOutOfOrder = v.AllowsSpecificInstallmentOutOfOrder,
        CommercialNoteRecalculation = v.CommercialNoteRecalculation,
        AdvanceOptionFirst = v.AdvanceOptionFirst,
        PaymentForms = [.. v.PaymentForms],
        DefaultPaymentForm = v.DefaultPaymentForm,
        Requirements = new RequisitosRequest
        {
            MinAssociateSeniorityMonths = v.MinAssociateSeniorityMonths, MinAge = v.MinAge, MaxAge = v.MaxAge, RequiresEmployment = v.RequiresEmployment,
        },
        CreditLimit = new CupoYAportesRequest
        {
            ContributionsMultiple = v.ContributionsMultiple, MaxIndebtedness = v.MaxIndebtedness, MinContributions = v.MinContributionsBalance,
        },
        RequiresBureauQuery = v.RequiresBureauQuery,
        InterestTaxConcept = v.InterestTaxTreatment is { } t
            ? new ConceptoTributarioRequest { Taxed = t == 2, SupportAttachmentPublicId = v.InterestTaxSupportAttachmentPublicId }
            : null,
        Documents = [.. v.Documents.OrderBy(d => d.Order).Select(d => new DocumentoExigidoRequest
        {
            Code = d.Code, Name = d.Name, Mandatory = d.Mandatory, AppliesTo = d.AppliesTo, AmountFrom = d.AmountFrom, AmountTo = d.AmountTo,
            DebtorKind = d.DebtorKind, PaymentForm = d.PaymentForm, MaxAgeDays = d.MaxAgeDays,
        })],
        Insurances = [.. v.Insurances.Select(s => new SeguroRequest
        {
            Code = s.Code, Name = s.Name, InsurerPersonPublicId = s.InsurerPersonPublicId, Base = s.Base, Rate = s.Rate, Periodicity = s.Periodicity,
            Mandatory = s.Mandatory, RetainedFraction = s.RetainedFraction, SelfInsured = s.SelfInsured, IsDebtorLifeInsurance = s.IsDebtorLifeInsurance,
            AccrualTiming = s.AccrualTiming,
        })],
        CollateralRules = [.. v.CollateralRules.OrderBy(g => g.Order).Select(g => new ReglaDeGarantiaRequest
        {
            AmountFrom = g.AmountFrom, AmountTo = g.AmountTo, TermFrom = g.TermFrom, TermTo = g.TermTo, CoDebtors = g.CoDebtors,
            AdmittedKinds = [.. g.AdmittedKinds], MinCoverage = g.MinCoverage, ContributionsCount = g.ContributionsCount,
            PromissoryNoteKind = g.PromissoryNoteKind,
        })],
    };
}

/// <summary>El alta de una línea: código de la cooperativa, nombre, destino y su primera versión.</summary>
public sealed record CrearLineaRequest(string Code, string Name, string? Purpose, VersionDeLineaRequest FirstVersion);

/// <summary>La consulta de usura: todo opcional (por defecto hoy, el monto y el plazo máximos y la periodicidad por defecto).</summary>
public sealed record ConsultaDeUsuraRequest(DateOnly? Date = null, decimal? Amount = null, int? TermInstallments = null, int? Periodicity = null);

/// <summary>Un rango de puntos de una variable.</summary>
public sealed record RangoDePuntajeDto(decimal? From, decimal? To, decimal Points);

/// <summary>Una variable del bloque de puntaje: su fuente es de la lista cerrada que expone el motor.</summary>
public sealed record VariableDePuntajeDto(string Code, string Name, string Source, decimal Weight, IReadOnlyList<RangoDePuntajeDto> Ranges);

/// <summary>El cuerpo de <c>POST /{id}/scoring</c>: crea una versión que copia todo y cambia sólo el puntaje.</summary>
public sealed record PuntajeRequest(DateOnly ValidFrom, string Reason, decimal? MinimumScore, string? ExceptionLevelCode,
    IReadOnlyList<VariableDePuntajeDto> Variables);

/// <summary>Una equivalencia que se registra: <c>Kind</c> 1 periodicidad, 2 unidad de plazo.</summary>
public sealed record EquivalenciaDeComercioRequest(int Kind, string CommercialCode, int? Periodicity = null, int? DaysPerUnit = null);

/// <summary>El cuerpo de <c>PUT /commercial-term-equivalences</c>.</summary>
public sealed record EquivalenciasDeComercioRequest(IReadOnlyList<EquivalenciaDeComercioRequest> Items, DateOnly ValidFrom, string Reason);

// ------------------------------------------------------------------------------------------------------- salida --

public sealed record BandaDeTasaDto(int Order, decimal? AmountFrom, decimal? AmountTo, int? TermFrom, int? TermTo, int? DebtorKind,
    bool? ForEmployees, int? MinSeniorityMonths, decimal? EffectiveAnnual, decimal? Margin);

public sealed record DocumentoExigidoDto(string Code, string Name, bool Mandatory, int? AppliesTo, decimal? AmountFrom, decimal? AmountTo,
    int? DebtorKind, int? PaymentForm, int? MaxAgeDays, int Order);

public sealed record SeguroDto(string Code, string Name, Guid InsurerPersonPublicId, string? InsurerName, int Base, decimal Rate, int Periodicity,
    int AccrualTiming, bool Mandatory, decimal? RetainedFraction, bool SelfInsured, bool IsDebtorLifeInsurance, bool ComputesForUsury);

public sealed record ReglaDeGarantiaDto(int Order, decimal? AmountFrom, decimal? AmountTo, int? TermFrom, int? TermTo, int CoDebtors,
    IReadOnlyList<int> AdmittedKinds, decimal? MinCoverage, bool ContributionsCount, int? PromissoryNoteKind);

/// <summary>Una versión completa; <c>ValidTo</c> es la víspera de la siguiente.</summary>
public sealed record VersionDeLineaDto(
    Guid PublicId,
    int VersionNumber,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateTime RegisteredAt,
    string? RegisteredBy,
    string Reason,
    string ModalityCode,
    string? SubsegmentCode,
    bool ForEmployees,
    bool AllowsNonAssociates,
    bool AllowsCommercialOrigin,
    int? CommercialDefaultFor,
    int? InterestTaxTreatment,
    Guid? InterestTaxSupportAttachmentPublicId,
    int RateKind,
    decimal? EffectiveAnnual,
    string? IndexCode,
    string? IndexKey,
    decimal? Margin,
    decimal? RateFloor,
    decimal? RateCeiling,
    int? ResetPeriodicity,
    int DefaultRateKind,
    decimal? DefaultRate,
    decimal? DefaultRateFactor,
    decimal MinAmount,
    decimal MaxAmount,
    int MinTermMonths,
    int MaxTermMonths,
    int? MaxInstallments,
    IReadOnlyList<int> Periodicities,
    int DefaultPeriodicity,
    byte? PaymentDay,
    byte? CutoffDay,
    IReadOnlyList<int> AmortizationSystems,
    int DefaultAmortizationSystem,
    bool AllowsExtraordinaryInstallments,
    decimal? MaxExtraordinaryFraction,
    int AllowedGraceKind,
    int? MaxGraceInstallments,
    int? GraceInterestTreatment,
    int? GraceDistributionInstallments,
    bool AllowsTranches,
    int DayCount,
    int RateEquivalence,
    decimal RoundingUnit,
    int RoundingMode,
    int RoundingMoment,
    int AllocationOrder,
    IReadOnlyList<int> ConceptOrder,
    bool SuspenseWithItsConcept,
    bool AllowsSpecificInstallmentOutOfOrder,
    int AdvanceOptionFirst,
    int CommercialNoteRecalculation,
    IReadOnlyList<int> PaymentForms,
    int DefaultPaymentForm,
    int? MinAssociateSeniorityMonths,
    int? MinAge,
    int? MaxAge,
    bool RequiresEmployment,
    decimal? ContributionsMultiple,
    decimal? MaxIndebtedness,
    decimal? MinContributionsBalance,
    bool RequiresBureauQuery,
    bool HasScoring,
    decimal? MinimumScore,
    string ContentSha256,
    IReadOnlyList<BandaDeTasaDto> RateBands,
    IReadOnlyList<DocumentoExigidoDto> Documents,
    IReadOnlyList<SeguroDto> Insurances,
    IReadOnlyList<ReglaDeGarantiaDto> CollateralRules)
{
    /// <summary>K6b: la unidad del capital, 1 pesos o 2 UVR.</summary>
    public int DenominationUnit { get; init; } = 1;
}

/// <summary>Una versión en el historial de la línea.</summary>
public sealed record ResumenDeVersionDto(Guid PublicId, int VersionNumber, DateOnly ValidFrom, DateOnly? ValidTo, DateTime RegisteredAt,
    string? RegisteredBy, string Reason, bool IsCurrent);

/// <summary>Una línea con su versión vigente en la fecha pedida y el historial. <c>Status</c> 1 borrador, 2 activa, 3 cerrada.</summary>
public sealed record LineaDeCreditoDto(
    Guid PublicId,
    string Code,
    string Name,
    string? Purpose,
    int Status,
    int? ClosedReason,
    DateTime? ClosedAt,
    string? CloseNotes,
    DateOnly? UsuryBlockedSince,
    Guid? UsuryBlockedByValuePublicId,
    bool AdmitsNewCredits,
    VersionDeLineaDto? CurrentVersion,
    IReadOnlyList<ResumenDeVersionDto> Versions)
{
    /// <summary>K6b: en una línea de vivienda, el tope de tasa y los límites de la Ley 546 vigentes con su fuente; nulo en las demás.</summary>
    public ViviendaDeLineaDto? Housing { get; init; }
}

/// <summary>Un límite de vivienda vigente (valor nacional <c>LIMITE</c>) con su fuente.</summary>
public sealed record LimiteDeViviendaDto(string Code, decimal Value, string? Source, Guid? NationalValuePublicId);

/// <summary>El tope de tasa de vivienda vigente (<c>TOPE_TASA_VIVIENDA</c>, EA).</summary>
public sealed record TopeDeViviendaDto(decimal Rate, DateOnly ValidFrom, string? Source, Guid NationalValuePublicId);

/// <summary>Las reglas de la Ley 546 que rigen la línea de vivienda a la fecha; <c>Missing</c> dice lo que no está registrado.</summary>
public sealed record ViviendaDeLineaDto(DateOnly Date, TopeDeViviendaDto? RateCap, IReadOnlyList<LimiteDeViviendaDto> Limits, IReadOnlyList<string> Missing);

/// <summary>La versión vigente resumida para la lista; <c>UsuryHeadroom</c> es la menor holgura de su plan tipo hoy.</summary>
public sealed record VersionVigenteResumidaDto(Guid PublicId, DateOnly ValidFrom, int RateKind, decimal? CurrentRate, string? IndexCode,
    decimal? Margin, decimal? UsuryHeadroom);

/// <summary>Una línea en la lista (sólo las del alcance de quien pregunta).</summary>
public sealed record LineaDeCreditoResumenDto(
    Guid PublicId,
    string Code,
    string Name,
    string? Purpose,
    int Status,
    int? ClosedReason,
    DateOnly? UsuryBlockedSince,
    string? ModalityCode,
    string? SubsegmentCode,
    bool CommercialOrigin,
    VersionVigenteResumidaDto? CurrentVersion);

/// <summary>Un cargo que computa para la usura.</summary>
public sealed record CargoDeUsuraDto(string Code, decimal Amount);

/// <summary>La consulta de usura: efectiva con y sin cargos, usura y su valor nacional, holgura y la explicación. No escribe.</summary>
public sealed record ConsultaDeUsuraDto(
    DateOnly Date,
    Guid VersionPublicId,
    string ModalityCode,
    string? UsuryKey,
    string Field,
    decimal Rate,
    decimal EffectiveRate,
    decimal EffectiveRateWithoutCharges,
    decimal ChargesSpread,
    decimal? UsuryRate,
    Guid? NationalValuePublicId,
    decimal? Headroom,
    IReadOnlyList<CargoDeUsuraDto> ComputableCharges,
    IReadOnlyList<string> Explanation,
    bool Passes);

/// <summary>El bloque de puntaje de la versión vigente con las fuentes que admite el motor.</summary>
public sealed record PuntajeDeLineaDto(Guid VersionPublicId, DateOnly ValidFrom, decimal? MinimumScore, string? ExceptionLevelCode,
    IReadOnlyList<VariableDePuntajeDto> Variables, IReadOnlyList<string> AllowedSources);

/// <summary>Una combinación modalidad + sub-segmento del catálogo nacional.</summary>
public sealed record CombinacionDeModalidadDto(string? SubsegmentCode, string SarcSegment, string? ExpectedLossSegment, string? UsuryKey,
    bool HousingRateCap, bool AdmitsUvr);

/// <summary>Una modalidad del catálogo nacional vigente.</summary>
public sealed record ModalidadDto(string Code, string Name, IReadOnlyList<string> Subsegments, string SarcSegment, string? ExpectedLossSegment,
    string? UsuryKey, bool AdmitsUvr, DateOnly? RetiredFrom, bool Verified, IReadOnlyList<CombinacionDeModalidadDto> Combinations);

/// <summary>El documento <c>CatalogoDeModalidades</c> vigente con su huella.</summary>
public sealed record CatalogoDeModalidadesDto(Guid DocumentPublicId, string Code, DateOnly ValidFrom, string Sha256, bool Verified,
    IReadOnlyList<ModalidadDto> Modalities);

/// <summary>Una equivalencia de Comercio vigente.</summary>
public sealed record EquivalenciaDeComercioDto(Guid PublicId, int Kind, string CommercialCode, int? Periodicity, int? DaysPerUnit, DateOnly ValidFrom,
    int VersionNumber, DateTime RegisteredAt);

/// <summary>Los filtros de la lista de líneas.</summary>
public sealed record FiltrosDeLineas(int? Status = null, string? Modality = null, bool? CommercialOrigin = null, bool? IncludeClosed = null);
