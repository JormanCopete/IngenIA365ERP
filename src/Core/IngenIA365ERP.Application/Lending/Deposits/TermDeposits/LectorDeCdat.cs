using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits;

/// <summary>Adónde van los intereses (y el vencimiento) de un CDAT: una cuenta de ahorro del titular o una orden de giro (api.md §5). (nuevo)</summary>
public enum TermDepositDestinationKind
{
    Account = 1,
    PaymentOrder = 2,
}

/// <summary>Cómo se fondea un CDAT: por un recaudador (Recaudo o Tesorería, por el contrato de cuentas) o desde otra cuenta del titular (api.md §5). (nuevo)</summary>
public enum TermDepositFundingKind
{
    Collector = 1,
    TransferFromAccount = 2,
}

/// <summary>El destino pedido, ya resuelto: la cuenta de ahorro o el beneficiario del giro (con su cuenta bancaria). (nuevo)</summary>
public sealed record DestinoDeCdat(
    TermDepositDestinationKind Kind,
    Guid? AccountPublicId,
    Guid? PayeePersonPublicId,
    Guid? BankPublicId,
    byte? BankAccountType,
    string? BankAccountNumber,
    Guid? ThirdPartyAuthorizationAttachmentPublicId = null);

/// <summary>La negociación sellada en la propuesta: el margen, la regla, quien la aprobó, el motivo y la solicitud si la hubo. (nuevo)</summary>
public sealed record NegociacionDeCdat(decimal Margen, int ReglaId, Guid ReglaPublicId, int? AprobadaPor, string Motivo, int? SolicitudId);

/// <summary>
/// La propuesta de un CDAT que espera el fondeo (feature 014, US6, T170; research E135): lo pedido y cotizado al constituir, guardado en
/// <c>LND_MemberAccounts.PendingTermsJson</c>. Al fondear se sella en la versión 1 de condiciones con el plan desde la fecha del fondeo. (nuevo)
/// </summary>
public sealed record PropuestaDeCdat(
    decimal Monto,
    int PlazoDias,
    RepaymentPeriodicity Periodicidad,
    InterestTiming Modalidad,
    DateOnly FechaPedida,
    int VersionDeLineaId,
    decimal TasaDeTabla,
    int? TramoId,
    DestinoDeCdat Destino,
    MaturityInstruction Instruccion,
    TermDepositFundingKind Fondeo,
    Guid? CuentaOrigenPublicId,
    NegociacionDeCdat? Negociacion,
    string? Canal,
    DateTime CotizadaEl)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public decimal TasaAplicada => TasaDeTabla + (Negociacion?.Margen ?? 0m);

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    public static PropuestaDeCdat? Leer(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PropuestaDeCdat>(json, Json);
}

/// <summary>La instrucción al vencimiento que cambió el titular (research E136): la nueva, su destino y quién la dio. (nuevo)</summary>
public sealed record InstruccionDeCdat(MaturityInstruction Instruccion, DestinoDeCdat? Destino, int? Usuario, DateTime Dada, string Motivo)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    public static InstruccionDeCdat? Leer(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<InstruccionDeCdat>(json, Json);
}

/// <summary>Un CDAT con sus versiones de condiciones y el plan de cada una (también lo agregado a la unidad de trabajo sin guardar). (nuevo)</summary>
public sealed record CdatLeido(MemberAccount Cuenta, IReadOnlyList<MemberAccountTermsVersion> Versiones,
    IReadOnlyDictionary<int, IReadOnlyList<TermDepositScheduleItem>> Planes)
{
    public MemberAccountTermsVersion? Vigente => Versiones.MaxBy(v => v.VersionNumber);

    public PropuestaDeCdat? Propuesta => PropuestaDeCdat.Leer(Cuenta.PendingTermsJson);

    public IReadOnlyList<TermDepositScheduleItem> PlanDe(MemberAccountTermsVersion v) => Planes.GetValueOrDefault(v.Id) ?? [];
}

/// <summary>
/// Lo que el CDAT lee y calcula fuera del motor (feature 014, US6, T170; research R11, E128): sus versiones y su plan, el calendario hábil (sábado, domingo y
/// los <c>FESTIVO</c> replicados, como el canje, E82), las condiciones del motor desde una versión sellada, la tasa de la tabla para un plazo y un monto, y las
/// filas del plan y de la versión que sella <c>RegistroDeCuentas</c>. No escribe. (nuevo)
/// </summary>
public sealed class LectorDeCdat(IApplicationDbContext db, ICalendarioHabil calendario, LectorDeInteres? interes = null)
{
    /// <summary>El código de la tasa de un tramo que no casa (para la explicación).</summary>
    public const string SinTramo = "version";

    public async Task<CdatLeido> LeerAsync(MemberAccount cuenta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        var versiones = cuenta.Id == 0
            ? []
            : await db.MemberAccountTermsVersions.AsNoTracking().Where(v => v.MemberAccountId == cuenta.Id && !v.IsDeleted).ToListAsync(ct);
        versiones.AddRange(db.MemberAccountTermsVersions.Local.Where(v => v.Id == 0 && v.MemberAccountId == cuenta.Id));
        var ids = versiones.Where(v => v.Id > 0).Select(v => v.Id).ToList();
        var planes = await db.TermDepositScheduleItems.AsNoTracking().Where(p => ids.Contains(p.TermsVersionId) && !p.IsDeleted).ToListAsync(ct);
        var porVersion = planes.GroupBy(p => p.TermsVersionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TermDepositScheduleItem>)g.OrderBy(p => p.PeriodNumber).ToList());
        return new CdatLeido(cuenta, versiones.OrderBy(v => v.VersionNumber).ToList(), porVersion);
    }

    /// <summary>El calendario hábil de <paramref name="desde"/> a <paramref name="hasta"/> con un margen para las fechas de pago y la gracia.</summary>
    public Task<CalendarioHabil> CalendarioAsync(DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        calendario.ObtenerAsync(desde.AddDays(-(int)RepaymentPeriodicity.Monthly), hasta.AddDays((int)RepaymentPeriodicity.Monthly), DiasDeDescanso.SabadoYDomingo,
            null, ct);

    /// <summary>Las condiciones del motor de una versión sellada.</summary>
    public static CondicionesDeCdat Condiciones(MemberAccountTermsVersion v) => new(v.PrincipalAmount, v.AppliedAnnualRate,
        v.StartDate ?? v.EffectiveValueDate, v.TermDays ?? 0, v.InterestPeriodicity ?? RepaymentPeriodicity.SinglePayment, v.InterestTiming ?? InterestTiming.Arrears,
        v.DayCount, new RedondeoDeCartera(v.RoundingUnit, v.RoundingMode, v.RoundingMoment));

    /// <summary>Las condiciones del motor de una propuesta, con la línea de la versión y el inicio dado.</summary>
    public static CondicionesDeCdat Condiciones(PropuestaDeCdat p, DepositLineVersion linea, DateOnly inicio) => new(p.Monto, p.TasaAplicada, inicio, p.PlazoDias,
        p.Periodicidad, p.Modalidad, linea.DayCount, linea.Redondeo());

    /// <summary>El plan de una versión sellada (el motor lo recalcula; lo guardado es su proyección).</summary>
    public async Task<PlanDeCdatCalculado> PlanAsync(MemberAccountTermsVersion v, CancellationToken ct)
    {
        var c = Condiciones(v);
        var cal = await CalendarioAsync(c.Inicio, PlanDeCdat.Vencimiento(c.Inicio, c.PlazoDias).AddDays((v.GraceBusinessDays ?? 0) * (int)RepaymentPeriodicity.Weekly), ct);
        return PlanDeCdat.Generar(c, cal);
    }

    /// <summary>
    /// La tasa de la tabla para un plazo y un monto (data-model §2.2): el primer tramo por orden que casa el plazo y el monto (sus nulos no restringen; los
    /// tramos sólo de saldo no aplican al CDAT) o, sin tramo, la de la versión. Con tasa variable, la de la versión con su índice del día (los márgenes de
    /// los tramos variables quedan fuera, research E129). Sin tasa: <c>Lending.TermDeposit.NoRateForTerm</c>.
    /// </summary>
    public async Task<Result<(decimal Tasa, DepositLineRateTier? Tramo)>> TasaDeLaTablaAsync(DepositLineVersion version, int plazo, decimal monto, DateOnly fecha,
        CancellationToken ct)
    {
        var tramos = await db.DepositLineRateTiers.AsNoTracking().Where(t => t.DepositLineVersionId == version.Id && !t.IsDeleted).OrderBy(t => t.Order)
            .ToListAsync(ct);
        if (version.RateKind == RateKind.Variable)
        {
            if (interes is null) return Result.Failure<(decimal, DepositLineRateTier?)>(ErroresDeCdat.SinTasa(plazo, monto));
            var leida = await interes.TasaAsync(new VersionConTramos(version, []), fecha, ct);
            return leida.IsFailure ? Result.Failure<(decimal, DepositLineRateTier?)>(leida.Error) : Result.Success<(decimal, DepositLineRateTier?)>((leida.Value.TasaEA, null));
        }
        var tramo = TramoDelCdat(tramos, plazo, monto);
        if (tramo is { AnnualRate: { } t }) return Result.Success<(decimal, DepositLineRateTier?)>((t, tramo));
        return version.AnnualRate is { } deLaVersion
            ? Result.Success<(decimal, DepositLineRateTier?)>((deLaVersion, null))
            : Result.Failure<(decimal, DepositLineRateTier?)>(ErroresDeCdat.SinTasa(plazo, monto));
    }

    /// <summary>El primer tramo de plazo o monto que casa (ver <see cref="TasaDeLaTablaAsync"/>).</summary>
    public static DepositLineRateTier? TramoDelCdat(IEnumerable<DepositLineRateTier> tramos, int plazo, decimal monto) =>
        tramos.Where(EsDePlazoOMonto).OrderBy(t => t.Order).FirstOrDefault(t =>
            (t.MinTermDays is null || plazo >= t.MinTermDays) && (t.MaxTermDays is null || plazo <= t.MaxTermDays)
            && (t.MinAmount is null || monto >= t.MinAmount) && (t.MaxAmount is null || monto <= t.MaxAmount));

    private static bool EsDePlazoOMonto(DepositLineRateTier t) => t.MinTermDays is not null || t.MaxTermDays is not null || t.MinAmount is not null || t.MaxAmount is not null;

    /// <summary>Las filas del plan de una versión (proyección del motor, data-model §3.4).</summary>
    public static IReadOnlyList<TermDepositScheduleItem> FilasDelPlan(MemberAccountTermsVersion version, PlanDeCdatCalculado plan) =>
        plan.Periodos.Select(p => new TermDepositScheduleItem
        {
            TermsVersion = version, TermsVersionId = version.Id, PeriodNumber = p.Numero, PeriodStart = p.Desde, PeriodEnd = p.Hasta, PaymentDate = p.FechaDePago,
            Days = p.Dias, PeriodRate = p.TasaDelPeriodo, Interest = p.Interes, IsLastPeriod = p.EsUltimo,
        }).ToList();

    /// <summary>
    /// Una versión de condiciones con lo de la versión de la línea (vencimiento, prórroga, gracia, redención), el plan y lo negociado. La causa, la operación y
    /// la versión anterior las pone quien la sella.
    /// </summary>
    public static MemberAccountTermsVersion NuevaVersion(MemberAccount cuenta, int numero, Domain.Entities.Lending.Deposits.Transactions.MemberAccountOperation operacion,
        AccountTermsCause causa, DepositLineVersion linea, PlanDeCdatCalculado plan, decimal tasaDeTabla, NegociacionDeCdat? negociacion,
        DestinoDeCdat destino, int? destinoCuentaId, int? destinoPersonaId, int? destinoBancoId, MaturityInstruction instruccion, int prorrogas,
        int? anterior, DateTime ahora, string huella, decimal? margenSinRegla = null) => new()
    {
        MemberAccountId = cuenta.Id,
        VersionNumber = numero,
        Operation = operacion,
        OperationId = operacion.Id,
        EffectiveValueDate = operacion.ValueDate,
        RegisteredAt = ahora,
        Cause = causa,
        DepositLineVersionId = linea.Id,
        RulesVersion = VersionesDeReglas.Vigente,
        InputsSha256 = huella,
        PrincipalAmount = plan.Condiciones.Capital,
        TermDays = plan.Condiciones.PlazoDias,
        StartDate = plan.Condiciones.Inicio,
        MaturityDate = plan.Vencimiento,
        PaymentDate = plan.FechaDePagoAlVencimiento,
        IssueTermBand = plan.PlazoDeEmision,
        TableAnnualRate = tasaDeTabla,
        // US6 (migración, research E148): un margen migrado no nombra regla (la negoció el sistema anterior).
        NegotiatedSpread = negociacion?.Margen ?? margenSinRegla,
        AppliedAnnualRate = tasaDeTabla + (negociacion?.Margen ?? margenSinRegla ?? 0m),
        NegotiatedRateRuleId = negociacion?.ReglaId,
        NegotiatedRateRequestId = negociacion?.SolicitudId,
        NegotiationApprovedByUserId = negociacion?.AprobadaPor,
        NegotiationReason = negociacion is null ? null : Recortar(negociacion.Motivo, MemberAccountTermsVersion.LargoDelMotivo),
        PeriodRate = plan.TasaDiaria,
        InterestPeriodicity = plan.Condiciones.Periodicidad,
        InterestTiming = plan.Condiciones.Modalidad,
        DayCount = plan.Condiciones.Convencion,
        RateEquivalence = RateEquivalence.Compound,
        RoundingUnit = plan.Condiciones.Redondeo.Unidad,
        RoundingMode = plan.Condiciones.Redondeo.Modo,
        RoundingMoment = plan.Condiciones.Redondeo.Momento,
        InterestDestination = destino.Kind == TermDepositDestinationKind.Account ? InterestDestination.ToAccount : InterestDestination.PaymentOrder,
        DestinationAccountId = destinoCuentaId,
        DestinationPersonId = destinoPersonaId,
        DestinationBankId = destinoBancoId,
        DestinationAccountType = destino.BankAccountType,
        DestinationBankAccountNumber = destino.BankAccountNumber,
        MaturityInstruction = instruccion,
        AutoRollover = linea.AutoRollover,
        MaxRollovers = linea.MaxRollovers,
        RolloverCount = prorrogas,
        RolloverRateRule = linea.RolloverRateRule,
        KeepsNegotiatedMargin = linea.KeepsNegotiatedMarginOnRollover,
        CapitalizesOnRollover = linea.CapitalizesOnRollover,
        GraceBusinessDays = linea.GraceBusinessDays,
        GraceAccrues = linea.GraceAccrues,
        AfterMaturityTreatment = linea.AfterMaturityTreatment,
        EarlyRedemptionAllowed = linea.EarlyRedemptionAllowed,
        EarlyRedemptionPenaltyMode = linea.EarlyRedemptionPenaltyMode,
        PenaltyAnnualRate = linea.PenaltyAnnualRate,
        PenaltyDiscountFraction = linea.PenaltyDiscountFraction,
        MinDaysBeforeRedemption = linea.MinDaysBeforeRedemption,
        PreviousVersionId = anterior,
    };

    public static string Recortar(string texto, int largo)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}
