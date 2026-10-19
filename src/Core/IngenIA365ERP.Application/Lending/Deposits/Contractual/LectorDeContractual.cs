using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Rates;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Contractual;

/// <summary>
/// Un ahorro contractual con sus versiones de condiciones (feature 014, US7, T182): la inicial —la de la apertura o la migrada— fija el contrato (cuota,
/// periodicidad, plazo, tasa base, bonificada y de castigo, tolerancia, efecto); las siguientes sólo cambian la tasa aplicada (el castigo desde el
/// incumplimiento). También lo agregado a la unidad de trabajo sin guardar. (nuevo)
/// </summary>
public sealed record ContractualLeido(MemberAccount Cuenta, IReadOnlyList<MemberAccountTermsVersion> Versiones)
{
    /// <summary>La versión que fija el contrato: la primera.</summary>
    public MemberAccountTermsVersion? Inicial => Versiones.MinBy(v => v.VersionNumber);

    /// <summary>La última versión (la del incumplimiento, si lo hubo).</summary>
    public MemberAccountTermsVersion? Vigente => Versiones.MaxBy(v => v.VersionNumber);

    /// <summary>
    /// La versión cuya tasa causa el día <paramref name="dia"/>: la última cuya fecha valor es anterior al día (el día del incumplimiento causa todavía a la
    /// tasa anterior: el interés del día d es el de (d − 1, d]) o, antes de todas, la inicial.
    /// </summary>
    public MemberAccountTermsVersion? DelDia(DateOnly dia) => Versiones.Where(v => v.EffectiveValueDate < dia).MaxBy(v => v.VersionNumber) ?? Inicial;
}

/// <summary>Una cuota del plan con lo que la cuenta tiene de ella: la cuota exigible generada (si ya se generó) con lo pagado. (nuevo)</summary>
public sealed record CuotaDelContrato(CuotaDeContractual DelPlan, CuotaLeida? Generada)
{
    public decimal Pagado => Generada?.Pagado ?? 0m;

    public decimal Pendiente => Generada?.Pendiente ?? 0m;

    public bool Anulada => Generada?.Cuota.Anulada ?? false;
}

/// <summary>
/// Lo que el ahorro contractual lee y calcula fuera del motor (feature 014, US7, T182; research §19): sus versiones de condiciones, el plan del contrato desde la
/// versión inicial (<see cref="AhorroContractual"/>), los aportes del contrato para la bonificación y la reliquidación (las consignaciones y traslados de capital
/// con su fecha valor y, en un contrato migrado, las cuotas pagadas antes del corte fechadas en su vencimiento del plan), las cuotas exigibles por número del
/// plan con lo pagado, los días de gracia de la cooperativa y el cumplimiento. No escribe. (nuevo)
/// </summary>
public sealed class LectorDeContractual(IApplicationDbContext db, ReglasDeParametrosDeCartera parametros, LectorDeInteres? interes = null)
{
    /// <summary>Las clases de fila del libro que son aportes al contrato: la consignación y el traslado que entra (sus espejos restan con la misma clase).</summary>
    public static IReadOnlyList<MemberAccountTransactionKind> ClasesDeAporte { get; } = [MemberAccountTransactionKind.Deposit, MemberAccountTransactionKind.TransferIn];

    public async Task<ContractualLeido> LeerAsync(MemberAccount cuenta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        var versiones = cuenta.Id == 0
            ? []
            : await db.MemberAccountTermsVersions.AsNoTracking().Where(v => v.MemberAccountId == cuenta.Id && !v.IsDeleted).ToListAsync(ct);
        versiones.AddRange(db.MemberAccountTermsVersions.Local.Where(v => v.Id == 0 && v.MemberAccountId == cuenta.Id));
        return new ContractualLeido(cuenta, versiones.OrderBy(v => v.VersionNumber).ToList());
    }

    /// <summary>Las condiciones del motor desde la versión inicial (la tasa base es la aplicada al abrir: la de la tabla más el margen negociado).</summary>
    public static CondicionesDeContractual Condiciones(MemberAccountTermsVersion inicial) => new(
        inicial.InstallmentAmount ?? 0m,
        inicial.InstallmentPeriodicity ?? RepaymentPeriodicity.Monthly,
        inicial.TermMonths ?? 1,
        inicial.StartDate ?? inicial.EffectiveValueDate,
        inicial.AppliedAnnualRate,
        inicial.BonusAnnualRate,
        inicial.BreachAnnualRate,
        inicial.DayCount,
        new RedondeoDeCartera(inicial.RoundingUnit, inicial.RoundingMode, inicial.RoundingMoment));

    /// <summary>El plan del contrato desde su versión inicial.</summary>
    public static PlanDeContractualCalculado Plan(MemberAccountTermsVersion inicial) => AhorroContractual.Plan(Condiciones(inicial));

    /// <summary>
    /// La tasa base del contrato al abrir (FR-074): la variable del día; con tramos por saldo, la del tramo que contiene lo pactado (cuota × cuotas); si no,
    /// la de la versión. Sin tasa: <c>Lending.TermDeposit.NoRateForTerm</c> con el monto pactado.
    /// </summary>
    public async Task<Result<decimal>> TasaBaseAsync(DepositLineVersion version, decimal pactado, DateOnly fecha, CancellationToken ct)
    {
        if (version.RateKind == RateKind.Variable)
        {
            if (interes is null) return Result.Failure<decimal>(TermDeposits.ErroresDeCdat.SinTasa(0, pactado));
            var leida = await interes.TasaAsync(new VersionConTramos(version, []), fecha, ct);
            return leida.IsFailure ? Result.Failure<decimal>(leida.Error) : Result.Success(leida.Value.TasaEA);
        }
        var tramos = await db.DepositLineRateTiers.AsNoTracking()
            .Where(t => t.DepositLineVersionId == version.Id && !t.IsDeleted && t.MinTermDays == null && t.MaxTermDays == null && t.MinAmount == null
                        && t.MaxAmount == null)
            .OrderBy(t => t.Order).ToListAsync(ct);
        var tramo = tramos.FirstOrDefault(t => (t.MinBalance ?? 0m) <= pactado && (t.MaxBalance is null || pactado <= t.MaxBalance));
        if (tramo is { AnnualRate: { } delTramo }) return Result.Success(delTramo);
        return version.AnnualRate is { } deLaVersion ? Result.Success(deLaVersion) : Result.Failure<decimal>(TermDeposits.ErroresDeCdat.SinTasa(0, pactado));
    }

    /// <summary>
    /// Los aportes al contrato hasta <paramref name="hasta"/> (FR-024): las filas de capital de consignación y de traslado que entra con su fecha valor —sus
    /// espejos las restan—, también las de esta unidad de trabajo; en un contrato migrado, además, las cuotas pagadas antes del corte fechadas en su
    /// vencimiento del plan.
    /// </summary>
    public async Task<IReadOnlyList<AporteDeContractual>> AportesAsync(ContractualLeido leido, DateOnly hasta, CancellationToken ct)
    {
        var cuenta = leido.Cuenta;
        var clases = ClasesDeAporte.ToArray();
        var guardadas = cuenta.Id == 0
            ? []
            : await db.MemberAccountTransactions.AsNoTracking()
                .Where(t => t.MemberAccountId == cuenta.Id && t.Concept == MemberAccountConcept.Principal && clases.Contains(t.Kind) && t.ValueDate <= hasta
                            && !t.IsDeleted)
                .Select(t => new { t.ValueDate, t.Amount }).ToListAsync(ct);
        var aportes = guardadas.Select(t => new AporteDeContractual(t.ValueDate, t.Amount)).ToList();
        aportes.AddRange(db.MemberAccountTransactions.Local
            .Where(t => t.Id == 0 && t.MemberAccountId == cuenta.Id && t.Concept == MemberAccountConcept.Principal && clases.Contains(t.Kind) && t.ValueDate <= hasta)
            .Select(t => new AporteDeContractual(t.ValueDate, t.Amount)));
        if (leido.Inicial is { MigratedPaidInstallments: > 0 } migrada)
            aportes.AddRange(AhorroContractual.AportesDelPlan(Plan(migrada), migrada.MigratedPaidInstallments.Value).Where(a => a.Fecha <= hasta));
        return aportes.OrderBy(a => a.Fecha).ToList();
    }

    /// <summary>
    /// Las cuotas del plan con la exigible que les corresponde (por su vencimiento) y lo pagado. Las anuladas se devuelven marcadas. Las que no se generaron
    /// todavía van sin exigible.
    /// </summary>
    public async Task<IReadOnlyList<CuotaDelContrato>> CuotasAsync(MemberAccount cuenta, PlanDeContractualCalculado plan, CancellationToken ct)
    {
        var generadas = cuenta.Id == 0 ? [] : await LectorDeCuotas.DeCuentasAsync(db, [cuenta.Id], ct, incluirAnuladas: true);
        return plan.Cuotas.Select(q => new CuotaDelContrato(q, generadas.FirstOrDefault(g => g.Cuota.DueDate == q.Vencimiento))).ToList();
    }

    /// <summary>
    /// El cumplimiento a <paramref name="dia"/> (FR-074): sobre las cuotas generadas (las del plan que todavía no empiezan no cuentan), con la tolerancia
    /// sellada y los días de gracia de la cooperativa o de la línea.
    /// </summary>
    public async Task<CumplimientoDelContractual> CumplimientoAsync(MemberAccount cuenta, MemberAccountTermsVersion inicial, PlanDeContractualCalculado plan,
        IReadOnlyList<CuotaDelContrato> cuotas, DateOnly dia, CancellationToken ct)
    {
        var gracia = await DiasDeGraciaAsync(cuenta.CreditLineId, dia, ct);
        var medidas = cuotas.Where(c => c.Generada is not null)
            .Select(c => new CuotaParaCumplimiento(c.DelPlan.Numero, c.DelPlan.Vencimiento, c.Generada!.Cuota.Amount, c.Pagado, c.Anulada)).ToList();
        return AhorroContractual.Cumplimiento(medidas, plan.Cuotas.Count, dia, inicial.ToleratedLateInstallments ?? 0, gracia);
    }

    /// <summary>
    /// Los días de gracia de una cuota (<c>Captaciones.DiasDeGraciaDeCuotaContractual</c>, de la línea o de la cooperativa); sin lectura —antes de que la
    /// entrega K9 los habilite—, el defecto del catálogo.
    /// </summary>
    public async Task<int> DiasDeGraciaAsync(int lineaId, DateOnly fecha, CancellationToken ct)
    {
        var clave = ParametrosDeCartera.CaptacionesDiasDeGraciaDeCuotaContractual;
        var leido = await parametros.LeerAsync(clave, fecha, null, ParameterScopeKind.CreditLine, lineaId, ct);
        if (leido.IsSuccess && Entero(leido.Value) is { } dias and >= 0) return dias;
        var defecto = ParametrosDeCartera.Definiciones.First(d => d.Clave == clave).DefectoSeguro;
        return int.TryParse(defecto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    /// <summary>El saldo de la bonificación provisionada (concepto <c>Bonus</c> del libro, también lo de esta unidad de trabajo).</summary>
    public async Task<decimal> BonificacionProvisionadaAsync(int cuentaId, CancellationToken ct)
    {
        var guardado = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuentaId && t.Concept == MemberAccountConcept.Bonus && !t.IsDeleted)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        return guardado + db.MemberAccountTransactions.Local.Where(t => t.Id == 0 && t.MemberAccountId == cuentaId && t.Concept == MemberAccountConcept.Bonus)
            .Sum(t => t.Amount);
    }

    /// <summary>
    /// Una versión de condiciones del contractual: el contrato (de <paramref name="plan"/>), la tasa aplicada (la base al abrir, la de castigo al incumplir), la
    /// línea copiada, el destino de la instrucción al vencimiento y la versión anterior. La causa y la operación las pone quien la sella.
    /// </summary>
    public static MemberAccountTermsVersion NuevaVersion(MemberAccount cuenta, int numero, MemberAccountOperation operacion, AccountTermsCause causa,
        DepositLineVersion linea, PlanDeContractualCalculado plan, decimal tasaAplicada, BonusRecognition? reconocimiento, DestinoDeContractual? destino,
        int? anterior, DateTime ahora, string huella, int? pagadasMigradas = null)
    {
        var c = plan.Condiciones;
        return new MemberAccountTermsVersion
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
            PrincipalAmount = plan.TotalPactado,
            TermMonths = c.PlazoMeses,
            StartDate = c.Inicio,
            MaturityDate = plan.Vencimiento,
            PaymentDate = plan.Vencimiento,
            IssueTermBand = plan.PlazoDeEmision,
            TableAnnualRate = tasaAplicada,
            AppliedAnnualRate = tasaAplicada,
            PeriodRate = EquivalenciaDeTasas.TasaDiaria(tasaAplicada, c.Convencion, RateEquivalence.Compound, RepaymentPeriodicity.SinglePayment),
            InterestPeriodicity = linea.SettlementPeriodicity,
            InterestTiming = InterestTiming.Arrears,
            DayCount = c.Convencion,
            RateEquivalence = linea.RateEquivalence,
            RoundingUnit = c.Redondeo.Unidad,
            RoundingMode = c.Redondeo.Modo,
            RoundingMoment = c.Redondeo.Momento,
            InterestDestination = InterestDestination.Capitalize,
            DestinationAccountId = destino?.CuentaId,
            DestinationPersonId = destino?.PersonaId,
            DestinationBankId = destino?.BancoId,
            DestinationAccountType = destino?.TipoDeCuenta,
            DestinationBankAccountNumber = destino?.NumeroDeCuenta,
            MaturityInstruction = destino is null ? null : MaturityInstruction.CancelAndPay,
            InstallmentAmount = c.Cuota,
            InstallmentPeriodicity = c.Periodicidad,
            InstallmentCount = plan.Cuotas.Count,
            BonusAnnualRate = c.TasaBonificada,
            BreachAnnualRate = c.TasaCastigo,
            ToleratedLateInstallments = linea.ToleratedLateInstallments ?? 0,
            BreachEffect = linea.BreachEffect ?? ContractualBreachEffect.BreachRate,
            BonusRecognition = reconocimiento ?? linea.BonusRecognition ?? BonusRecognition.AtFulfillment,
            ProjectedFinalAmount = plan.ValorFinalCumpliendo,
            MigratedPaidInstallments = pagadasMigradas,
            PreviousVersionId = anterior,
        };
    }

    /// <summary>La versión de castigo: copia el contrato de la inicial y cambia la tasa aplicada a la de castigo (o la base, si la línea no la tiene).</summary>
    public static MemberAccountTermsVersion VersionDeCastigo(MemberAccount cuenta, MemberAccountTermsVersion inicial, MemberAccountTermsVersion ultima,
        MemberAccountOperation operacion, DateTime ahora, string huella)
    {
        var tasa = inicial.BreachAnnualRate ?? inicial.AppliedAnnualRate;
        return new MemberAccountTermsVersion
        {
            MemberAccountId = cuenta.Id,
            VersionNumber = ultima.VersionNumber + 1,
            Operation = operacion,
            OperationId = operacion.Id,
            EffectiveValueDate = operacion.ValueDate,
            RegisteredAt = ahora,
            Cause = AccountTermsCause.ContractualBreach,
            DepositLineVersionId = inicial.DepositLineVersionId,
            RulesVersion = VersionesDeReglas.Vigente,
            InputsSha256 = huella,
            PrincipalAmount = inicial.PrincipalAmount,
            TermMonths = inicial.TermMonths,
            StartDate = inicial.StartDate,
            MaturityDate = inicial.MaturityDate,
            PaymentDate = inicial.PaymentDate,
            IssueTermBand = inicial.IssueTermBand,
            TableAnnualRate = tasa,
            AppliedAnnualRate = tasa,
            PeriodRate = EquivalenciaDeTasas.TasaDiaria(tasa, inicial.DayCount, RateEquivalence.Compound, RepaymentPeriodicity.SinglePayment),
            InterestPeriodicity = inicial.InterestPeriodicity,
            InterestTiming = inicial.InterestTiming,
            DayCount = inicial.DayCount,
            RateEquivalence = inicial.RateEquivalence,
            RoundingUnit = inicial.RoundingUnit,
            RoundingMode = inicial.RoundingMode,
            RoundingMoment = inicial.RoundingMoment,
            InterestDestination = inicial.InterestDestination,
            DestinationAccountId = ultima.DestinationAccountId,
            DestinationPersonId = ultima.DestinationPersonId,
            DestinationBankId = ultima.DestinationBankId,
            DestinationAccountType = ultima.DestinationAccountType,
            DestinationBankAccountNumber = ultima.DestinationBankAccountNumber,
            MaturityInstruction = ultima.MaturityInstruction,
            InstallmentAmount = inicial.InstallmentAmount,
            InstallmentPeriodicity = inicial.InstallmentPeriodicity,
            InstallmentCount = inicial.InstallmentCount,
            BonusAnnualRate = inicial.BonusAnnualRate,
            BreachAnnualRate = inicial.BreachAnnualRate,
            ToleratedLateInstallments = inicial.ToleratedLateInstallments,
            BreachEffect = inicial.BreachEffect,
            BonusRecognition = inicial.BonusRecognition,
            ProjectedFinalAmount = null,
            MigratedPaidInstallments = inicial.MigratedPaidInstallments,
            PreviousVersionId = ultima.Id == 0 ? null : ultima.Id,
        };
    }

    private static int? Entero(ParametroDeCartera p) => p.Valor switch
    {
        int i => i,
        long l => (int)l,
        decimal d => (int)d,
        _ => int.TryParse(p.Texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null,
    };
}

/// <summary>El destino del vencimiento sellado en la versión: la cuenta de ahorro a la vista, o la persona del giro con su cuenta bancaria. (nuevo)</summary>
public sealed record DestinoDeContractual(int? CuentaId, int? PersonaId, int? BancoId, byte? TipoDeCuenta, string? NumeroDeCuenta);
