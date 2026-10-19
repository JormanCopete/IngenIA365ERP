using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Lending.Deposits.Withdrawals;

/// <summary>El descuento por pérdidas que decidió el acta (FR-078, D15): el punto, el año del ejercicio y la proporción sobre los aportes. (nuevo)</summary>
public sealed record LossDeductionInput(Guid SessionItemPublicId, int Year, decimal Proportion);

/// <summary>La forma de la devolución del remanente (api.md §11): por orden de giro a Tesorería en K7; a otra cuenta, con K8. (nuevo)</summary>
public sealed record MembershipRefundInput(FormaDeDevolucionDeAportes Form, Guid? TargetAccountPublicId = null, PayeeBankInput? Payee = null);

/// <summary>El cruce contra un crédito en la vista previa: su lugar en la prelación (D14), mora, tasa, lo que vale cancelarlo y lo que se aplica. (nuevo)</summary>
public sealed record MembershipWithdrawalOffsetDto(Guid LoanPublicId, string Number, int Priority, int DaysPastDue, decimal AnnualRate, decimal Payoff,
    decimal Amount, bool PaysOff);

/// <summary>El mínimo irreducible de la entidad frente a la liquidación (FR-080). (nuevo)</summary>
public sealed record EntityMinimumDto(decimal Capital, decimal? Minimum, decimal CapitalAfter, bool WouldBreach);

/// <summary>El descuento por pérdidas de la vista previa. (nuevo)</summary>
public sealed record LossDeductionDto(int Year, decimal Proportion, decimal Amount, Guid SessionItemPublicId);

/// <summary>La vista previa de la liquidación (api.md §11, <c>settlement/preview</c>), con la huella que la liquidación exige. (nuevo)</summary>
public sealed record MembershipWithdrawalSettlementPreviewDto(
    DateOnly Date,
    IReadOnlyList<MembershipWithdrawalLineDto> Lines,
    IReadOnlyList<MembershipWithdrawalOffsetDto> Offsets,
    LossDeductionDto? LossDeduction,
    decimal Contributions,
    decimal Savings,
    decimal Crossed,
    decimal Refund,
    DateOnly RefundDueOn,
    EntityMinimumDto EntityMinimum,
    string Sha256,
    IReadOnlyList<string> Warnings)
{
    /// <summary>D45 (2026-10-18): lo que los créditos siguen debiendo después del cruce; mayor que cero, quien liquida elige qué pasa con ese saldo.</summary>
    public decimal PendingLoanBalance { get; init; }

    /// <summary>D41 (2026-10-18): de qué fondos salió primero el cruce (<c>AportesPrimero</c> o <c>AhorrosPrimero</c>, según los estatutos).</summary>
    public string FundsOrder { get; init; } = nameof(OrdenDeFondosAlRetiro.AportesPrimero);
}

/// <summary>Lo que se liquida y con qué: el cálculo, las cuentas y los créditos, el mínimo de la entidad y la huella. (nuevo)</summary>
public sealed record PlanDeLiquidacionDeRetiro(
    LiquidacionDeRetiroCalculada Calculo,
    IReadOnlyDictionary<int, MemberAccount> Cuentas,
    IReadOnlyDictionary<int, (Guid PublicId, string Number)> Creditos,
    IReadOnlyList<int> Codeudores,
    EntityMinimumDto MinimoDeLaEntidad,
    DateOnly DevolverHasta,
    LossDeductionInput? Perdida,
    string Huella,
    IReadOnlyList<string> Avisos)
{
    /// <summary>D41: el orden de los fondos con que se calculó.</summary>
    public OrdenDeFondosAlRetiro OrdenDeFondos { get; init; } = OrdenDeFondosAlRetiro.AportesPrimero;

    public MembershipWithdrawalSettlementPreviewDto Dto(DateOnly fecha)
    {
        var lineas = Calculo.Lineas.Select((l, i) => new MembershipWithdrawalLineDto(0, i + 1, l.Clase,
            l.CuentaId is { } c ? Cuentas[c].PublicId : null, l.CuentaId is { } c2 ? Cuentas[c2].Number : null,
            l.CreditoId is { } k ? Creditos[k].PublicId : null, l.CreditoId is { } k2 ? Creditos[k2].Number : null,
            l.Concepto, l.Monto, null, null, l.Nota)).ToList();
        var cruces = Calculo.Cruces.Select(c => new MembershipWithdrawalOffsetDto(Creditos[c.Credito.CreditoId].PublicId, c.Credito.Numero, c.Prelacion,
            c.Credito.DiasDeMora, c.Credito.TasaAnual, c.Credito.ParaCancelar, c.Monto, c.Total)).ToList();
        var perdida = Perdida is { } p && Calculo.Perdidas > 0m ? new LossDeductionDto(p.Year, p.Proportion, Calculo.Perdidas, p.SessionItemPublicId) : null;
        return new MembershipWithdrawalSettlementPreviewDto(fecha, lineas, cruces, perdida, Calculo.Aportes, Calculo.Ahorros, Calculo.Cruzado,
            Calculo.ADevolver, DevolverHasta, MinimoDeLaEntidad, Huella, Avisos)
        {
            PendingLoanBalance = Calculo.SaldoPendienteDeCreditos,
            FundsOrder = OrdenDeFondos.ToString(),
        };
    }
}

/// <summary>
/// Arma la liquidación del retiro (feature 014, US10, T099; FR-076 a FR-080, D14, D15): sólo lee. Las cuentas que se liquidan son la de aportes del trámite
/// y las de ahorro vivas del asociado salvo los CDAT (siguen hasta su vencimiento, FR-076; aviso); los saldos son la suma del libro (los intereses a la fecha
/// los suma US5 y el CDAT, US6). Los créditos vivos son los de la persona como deudora, liquidados a la fecha por la reconstrucción del crédito
/// (<see cref="IMotorDeCreditoEnDesembolso.LiquidarAsync"/>), con la tasa de su última versión de condiciones. El descuento por pérdidas exige la línea
/// que lo admita (<c>LossDeductionEnabled</c>, D15) y un punto de acta cerrado que apruebe. El mínimo irreducible de la entidad
/// (<c>Aportes.MinimoIrreducibleEntidad</c>) se compara con el capital social después de todo lo que sale de aportes; si lo perfora, sólo se retiene la
/// devolución de los <b>aportes</b> (los ahorros se devuelven; decisión del dueño del 2026-10-18, D43). De qué fondos sale primero el cruce lo dicen los
/// estatutos (<c>Captaciones.OrdenDeFondosAlRetiro</c>, D41). La huella cubre todo lo que decide la liquidación: cambia con un pago, una consignación o un
/// saldo nuevo. (nuevo)
/// </summary>
public sealed class PlanesDeLiquidacionDeRetiro(
    IApplicationDbContext db,
    ReglasDeAportes reglas,
    IMotorDeCreditoEnDesembolso motor,
    OrdenesDeGiroDeCuentas ordenes,
    Accrual.LiquidacionDeIntereses? intereses = null)
{
    public async Task<Result<PlanDeLiquidacionDeRetiro>> ArmarAsync(MembershipWithdrawal retiro, DateOnly fecha, LossDeductionInput? perdida, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(retiro);
        var avisos = new List<string>();
        var aportes = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == retiro.ContributionAccountId, ct);
        var ahorros = await db.MemberAccounts.AsNoTracking()
            .Where(a => a.HolderPersonId == retiro.PersonId && a.Nature == LineNature.Deposit && !a.IsDeleted && a.Status != MemberAccountStatus.Closed)
            .OrderBy(a => a.Id).ToListAsync(ct);
        if (ahorros.Any(a => a.DepositClass == DepositClass.TermDeposit))
            avisos.Add("Los CDAT vigentes siguen hasta su vencimiento: no entran a esta liquidación (FR-076).");
        var cuentas = new List<MemberAccount> { aportes };
        cuentas.AddRange(ahorros.Where(a => a.DepositClass != DepositClass.TermDeposit));
        var saldos = await LectorDeCuentas.SaldosAsync(db, cuentas.Select(c => c.Id).ToList(), ct);
        // US5 (T136; FR-076): los ahorros que ganan interés entran con sus intereses liquidados a la fecha —el bruto abonado al capital, menos la retención,
        // y el interés por pagar en cero—, que es lo que la liquidación del retiro deja en el libro antes de cruzar y devolver.
        if (intereses is not null)
            foreach (var c in cuentas.Where(c => Accrual.LiquidacionDeIntereses.GanaInteres(c)
                                                 && c.Status is MemberAccountStatus.Active or MemberAccountStatus.Inactive))
            {
                var control = await db.MemberAccountControls.AsNoTracking().FirstAsync(x => x.MemberAccountId == c.Id, ct);
                var simulada = await intereses.CalcularAsync(c, control, fecha, DateTime.MaxValue, desdeElMotor: true, ct);
                if (simulada.IsFailure) return Result.Failure<PlanDeLiquidacionDeRetiro>(simulada.Error);
                var s = simulada.Value;
                if (s.Causado == 0m && s.Bruto == 0m) continue;
                var propios = saldos.TryGetValue(c.Id, out var leidos) ? leidos : saldos[c.Id] = [];
                propios[MemberAccountConcept.Principal] = propios.GetValueOrDefault(MemberAccountConcept.Principal) + s.Neto;
                propios[MemberAccountConcept.InterestPayable] = 0m;
                if (s.Retencion.SeRetiene)
                    propios[MemberAccountConcept.Withholding] = propios.GetValueOrDefault(MemberAccountConcept.Withholding) + s.Retencion.Valor;
                avisos.Add($"La cuenta {c.Number} entra con {s.Bruto:N2} de intereses liquidados a la fecha (retención {s.Retencion.Valor:N2}).");
            }
        var enElRetiro = cuentas.Select(c => new CuentaEnElRetiro(c.Id, c.Number, c.Nature, c.DepositClass,
            (IReadOnlyDictionary<MemberAccountConcept, decimal>?)saldos.GetValueOrDefault(c.Id) ?? new Dictionary<MemberAccountConcept, decimal>())).ToList();

        var vivos = await db.Loans.AsNoTracking()
            .Where(l => l.DebtorPersonId == retiro.PersonId && !l.IsDeleted && (l.Status == LoanStatus.Active || l.Status == LoanStatus.WrittenOff))
            .OrderBy(l => l.Id).Select(l => new { l.Id, l.PublicId, l.Number }).ToListAsync(ct);
        var creditos = new List<CreditoEnElRetiro>();
        foreach (var l in vivos)
        {
            var liquidacion = await motor.LiquidarAsync(l.Id, fecha, ct);
            var tasa = await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == l.Id).OrderByDescending(t => t.VersionNumber)
                .Select(t => t.AnnualRate).FirstOrDefaultAsync(ct) ?? 0m;
            creditos.Add(new CreditoEnElRetiro(l.Id, l.Number, liquidacion.ParaCancelar, liquidacion.DiasDeMora, tasa));
        }
        var ids = vivos.Select(l => l.Id).ToList();
        var codeudores = await db.LoanParties.AsNoTracking().Where(p => ids.Contains(p.LoanId) && p.Role == CreditPartyRole.CoDebtor && !p.IsDeleted)
            .Select(p => p.PersonId).Distinct().ToListAsync(ct);

        var version = await reglas.VersionAsync(aportes, fecha, ct);
        PerdidaEnElRetiro? deLaPerdida = null;
        if (perdida is not null)
        {
            if (version is not { LossDeductionEnabled: true }) return Result.Failure<PlanDeLiquidacionDeRetiro>(ErroresDeRetiro.PerdidasNoAdmitidas());
            var punto = await PuntoAprobadoAsync(perdida.SessionItemPublicId, ct);
            if (punto.IsFailure) return Result.Failure<PlanDeLiquidacionDeRetiro>(punto.Error);
            deLaPerdida = new PerdidaEnElRetiro(perdida.Year, perdida.Proportion);
        }

        var ordenDeFondos = await reglas.OrdenDeFondosAlRetiroAsync(fecha, ct);
        var calculo = LiquidacionDeRetiro.Calcular(enElRetiro, creditos, deLaPerdida, ordenDeFondos);
        var capital = await reglas.CapitalSocialAsync(ct);
        var minimo = await reglas.MinimoDeLaEntidadAsync(fecha, ct);
        var devolucionDeAportes = calculo.DevolucionDeAportes([aportes.Id]);
        var salenDeAportes = calculo.Lineas
            .Where(l => l.Clase is MembershipWithdrawalLineKind.LossDeduction or MembershipWithdrawalLineKind.LoanOffset && l.CuentaId == aportes.Id)
            .Sum(l => -l.Monto) + devolucionDeAportes;
        var despues = capital - salenDeAportes;
        // D43 (2026-10-18): el mínimo irreducible retiene sólo la parte de aportes de la devolución; los ahorros se devuelven.
        var perfora = minimo is > 0m && devolucionDeAportes > 0m && despues < minimo;
        if (perfora)
            avisos.Add($"{ErroresDeAportes.CodigoMinimoDeLaEntidad}: la devolución de los aportes dejaría el capital social ({despues:N2}) por debajo del mínimo "
                       + $"irreducible de la entidad ({minimo:N2}); los aportes quedan pendientes y se avisa al gerente (FR-080). Los ahorros se devuelven.");
        if (calculo.Cruces.Any(c => !c.Total)) avisos.Add("Los aportes y ahorros no alcanzan para todos los créditos: el que queda conserva su saldo y sus garantías.");
        if (calculo.SaldoPendienteDeCreditos > 0m)
            avisos.Add($"Los créditos siguen debiendo {calculo.SaldoPendienteDeCreditos:N2} después del cruce: al liquidar elija si pasan a recibir los pagos por "
                       + "caja o conservan su forma de pago (D45).");
        var devolverHasta = await ordenes.PlazoDeDevolucionAsync(retiro.EffectiveDate ?? fecha, version?.RefundTermDays, ct);

        var huella = new HuellaDeCartera()
            .Identificador("withdrawal", retiro.PublicId)
            .Entero("version", retiro.SettlementVersion)
            .Fecha("date", fecha)
            .Lista("accounts", enElRetiro, (h, c) => h.Entero("id", c.CuentaId)
                .Lista("balances", c.Saldos.OrderBy(s => (int)s.Key), (hh, s) => hh.Texto("concept", s.Key.ToString()).Decimal("amount", s.Value, 6)))
            .Lista("loans", creditos, (h, c) => h.Entero("id", c.CreditoId).Decimal("payoff", c.ParaCancelar, 2).Entero("daysPastDue", c.DiasDeMora)
                .Decimal("rate", c.TasaAnual, 6))
            .Si(perdida is not null, h => h.Identificador("lossItem", perdida!.SessionItemPublicId).Entero("lossYear", perdida.Year)
                .Decimal("lossProportion", perdida.Proportion, 6))
            .Decimal("capital", capital, 2)
            .Decimal("entityMinimum", minimo, 2)
            .Texto("fundsOrder", ordenDeFondos.ToString())
            .Calcular();
        return Result.Success(new PlanDeLiquidacionDeRetiro(calculo, cuentas.ToDictionary(c => c.Id),
            vivos.ToDictionary(l => l.Id, l => (l.PublicId, l.Number)), codeudores, new EntityMinimumDto(capital, minimo, despues, perfora), devolverHasta,
            deLaPerdida is null ? null : perdida, huella, avisos) { OrdenDeFondos = ordenDeFondos });
    }

    /// <summary>El punto de un acta cerrada que aprueba (cualquier asunto: el descuento por pérdidas lo dispone la asamblea o el consejo).</summary>
    private async Task<Result<Guid>> PuntoAprobadoAsync(Guid item, CancellationToken ct)
    {
        var p = await (from i in db.SessionItems.AsNoTracking()
                       join s in db.LendingSessions.AsNoTracking() on i.SessionId equals s.Id
                       where i.PublicId == item && !i.IsDeleted
                       select new { i.Kind, i.Outcome, i.SubjectType, s.Status }).FirstOrDefaultAsync(ct);
        var corresponde = p is not null && p.Status == SessionStatus.Closed && p.Kind == SessionItemKind.Decision
                          && p.Outcome is CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions;
        return corresponde
            ? Result.Success(item)
            : Result.Failure<Guid>(ErroresDeGobierno.PuntoNoCorresponde("LossDeduction", p?.SubjectType, p?.Outcome?.ToString()));
    }
}

/// <summary>
/// <c>POST /api/lending/membership-withdrawals/{id}/settlement/preview</c> (<c>PreviewMembershipWithdrawalSettlementQuery</c>,
/// <c>Lending.MembershipWithdrawals.Settle</c>, sin clave): la liquidación sin escribir, con su huella. (nuevo)
/// </summary>
public sealed record PreviewMembershipWithdrawalSettlementQuery(Guid WithdrawalPublicId, DateOnly? Date, LossDeductionInput? LossDeduction)
    : IRequest<Result<MembershipWithdrawalSettlementPreviewDto>>;

/// <summary>
/// <c>POST …/{id}/settle</c> (<c>SettleMembershipWithdrawalCommand</c>, <c>Lending.MembershipWithdrawals.Settle</c>): liquida el retiro aprobado (o por
/// rehacer) con la huella de la vista previa (si cambió, <c>Lending.Savings.PreviewChanged</c>) en <b>una sola operación</b> (FR-077): el descuento por
/// pérdidas, el cruce contra los créditos por la aplicación de pagos y la devolución del remanente por orden de giro (FR-079) —retenida si perforaría el
/// mínimo irreducible de la entidad (FR-080: el retiro se liquida, la devolución queda pendiente y se avisa al gerente)—; las líneas de la versión nueva y,
/// después, la liquidación emitida (<c>MembershipWithdrawalSettlement</c>). Sin nada que devolver, el retiro queda pagado y sus cuentas canceladas. (nuevo)
/// </summary>
/// <remarks>
/// Decisión del dueño del 2026-10-18 (D45): si los créditos siguen debiendo después del cruce, <paramref name="PendingLoanBalance"/> dice qué pasa con ese
/// saldo —pasan a caja (<c>MoveToCashier</c>, el defecto: las libranzas y la nómina pasan a la forma de pago alterna) o conservan su forma de pago—; se
/// guarda en el retiro. Sin deuda pendiente, no se usa.
/// </remarks>
public sealed record SettleMembershipWithdrawalCommand(Guid WithdrawalPublicId, DateOnly? Date, string ExpectedPreviewSha256,
    LossDeductionInput? LossDeduction, MembershipRefundInput Refund, string Reason, PendingLoanBalanceHandling? PendingLoanBalance = null)
    : IRequest<Result<MembershipWithdrawalDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

/// <summary>
/// <c>POST …/{id}/refund/approve</c> (<c>ApproveMembershipRefundCommand</c>, <c>Lending.MembershipWithdrawals.Approve</c>): ordena la devolución que quedó
/// pendiente —retenida por el mínimo irreducible de la entidad, que se vuelve a comparar, o reintegrada por una orden rechazada—; quien liquidó no la
/// aprueba (<c>Lending.Segregation.Conflict</c>). (nuevo)
/// </summary>
public sealed record ApproveMembershipRefundCommand(Guid WithdrawalPublicId, Guid? ApprovalPublicId, PayeeBankInput? Payee, string Reason)
    : IRequest<Result<MembershipWithdrawalDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class LossDeductionInputValidator : AbstractValidator<LossDeductionInput>
{
    public LossDeductionInputValidator()
    {
        RuleFor(x => x.SessionItemPublicId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Proportion).GreaterThan(0m).LessThanOrEqualTo(1m).WithMessage("La proporción de pérdidas va como fracción mayor que 0 y hasta 1.");
    }
}

public sealed class PreviewMembershipWithdrawalSettlementQueryValidator : AbstractValidator<PreviewMembershipWithdrawalSettlementQuery>
{
    public PreviewMembershipWithdrawalSettlementQueryValidator()
    {
        RuleFor(x => x.WithdrawalPublicId).NotEmpty();
        RuleFor(x => x.LossDeduction!).SetValidator(new LossDeductionInputValidator()).When(x => x.LossDeduction is not null);
    }
}

public sealed class SettleMembershipWithdrawalCommandValidator : ValidadorConMotivo<SettleMembershipWithdrawalCommand>
{
    public SettleMembershipWithdrawalCommandValidator()
    {
        RuleFor(x => x.WithdrawalPublicId).NotEmpty();
        RuleFor(x => x.ExpectedPreviewSha256).NotEmpty().WithMessage("Liquide con la huella de la vista previa.");
        RuleFor(x => x.Refund).NotNull();
        RuleFor(x => x.Refund.Form).IsInEnum().When(x => x.Refund is not null);
        RuleFor(x => x.Refund.Payee!.AccountNumber).NotEmpty().MaximumLength(25).When(x => x.Refund?.Payee is not null);
        RuleFor(x => x.LossDeduction!).SetValidator(new LossDeductionInputValidator()).When(x => x.LossDeduction is not null);
        RuleFor(x => x.PendingLoanBalance).IsInEnum().When(x => x.PendingLoanBalance is not null);
    }
}

public sealed class ApproveMembershipRefundCommandValidator : ValidadorConMotivo<ApproveMembershipRefundCommand>
{
    public ApproveMembershipRefundCommandValidator()
    {
        RuleFor(x => x.WithdrawalPublicId).NotEmpty();
        RuleFor(x => x.Payee!.AccountNumber).NotEmpty().MaximumLength(25).When(x => x.Payee is not null);
    }
}

public sealed class PreviewMembershipWithdrawalSettlementQueryHandler(
    RetirosDeAsociado retiros,
    IAlcanceDeCartera alcance,
    PlanesDeLiquidacionDeRetiro planes,
    IDateTimeService reloj) : IRequestHandler<PreviewMembershipWithdrawalSettlementQuery, Result<MembershipWithdrawalSettlementPreviewDto>>
{
    public async Task<Result<MembershipWithdrawalSettlementPreviewDto>> Handle(PreviewMembershipWithdrawalSettlementQuery request, CancellationToken ct)
    {
        var leido = await retiros.EnAlcanceAsync(alcance, request.WithdrawalPublicId, seguir: false, ct);
        if (leido.IsFailure) return Result.Failure<MembershipWithdrawalSettlementPreviewDto>(leido.Error);
        var retiro = leido.Value;
        if (!retiro.AdmiteLiquidar) return Result.Failure<MembershipWithdrawalSettlementPreviewDto>(ErroresDeRetiro.NoAprobado(retiro.Status));
        var hoy = reloj.HoyLocal;
        var fecha = request.Date ?? hoy;
        if (Fecha.Invalida(retiro, fecha, hoy) is { } mala) return Result.Failure<MembershipWithdrawalSettlementPreviewDto>(mala);
        var plan = await planes.ArmarAsync(retiro, fecha, request.LossDeduction, ct);
        return plan.IsFailure ? Result.Failure<MembershipWithdrawalSettlementPreviewDto>(plan.Error) : Result.Success(plan.Value.Dto(fecha));
    }
}

/// <summary>Las fechas de la liquidación: entre la fecha efectiva del retiro y hoy. (nuevo)</summary>
internal static class Fecha
{
    public static Error? Invalida(MembershipWithdrawal retiro, DateOnly fecha, DateOnly hoy)
    {
        var desde = retiro.EffectiveDate ?? retiro.FiledOn;
        if (desde > hoy) desde = hoy;
        return fecha < desde || fecha > hoy ? ErroresDeRetiro.FechaDeLiquidacion(fecha, desde, hoy) : null;
    }
}

public sealed class SettleMembershipWithdrawalCommandHandler(
    IApplicationDbContext db,
    RetirosDeAsociado retiros,
    IAlcanceDeCartera alcance,
    PlanesDeLiquidacionDeRetiro planes,
    CruceDeRetiro cruce,
    OrdenesDeGiroDeCuentas ordenes,
    RegistroDeIntervenciones intervenciones,
    ICerrojoDeCartera cerrojo,
    ISender sender,
    IActorActual actorActual,
    IDateTimeService reloj,
    ILogger<SettleMembershipWithdrawalCommandHandler> logger,
    IAlertas? alertas = null,
    Accrual.LiquidacionALaFecha? interesesALaFecha = null) : IRequestHandler<SettleMembershipWithdrawalCommand, Result<MembershipWithdrawalDto>>
{
    public async Task<Result<MembershipWithdrawalDto>> Handle(SettleMembershipWithdrawalCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        if ((await ActivacionesDeCaptacion.LeerAsync(db, ct)).Aportes is null) return Falla(ErroresDeCuentas.SinActivar(LineNature.Contribution));
        if (request.Refund.Form == FormaDeDevolucionDeAportes.TransferToAccount)
            return Falla(ErroresDeAportes.EntregaNoDisponible("La devolución del retiro a una cuenta de ahorro", nameof(EntregaDeCartera.K8)));
        var leido = await retiros.EnAlcanceAsync(alcance, request.WithdrawalPublicId, seguir: false, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        if (!leido.Value.AdmiteLiquidar) return Falla(ErroresDeRetiro.NoAprobado(leido.Value.Status));
        var fecha = request.Date ?? hoy;
        if (Fecha.Invalida(leido.Value, fecha, hoy) is { } mala) return Falla(mala);
        var visto = await planes.ArmarAsync(leido.Value, fecha, request.LossDeduction, ct);
        if (visto.IsFailure) return Falla(visto.Error);
        if (!string.Equals(visto.Value.Huella, request.ExpectedPreviewSha256, StringComparison.OrdinalIgnoreCase))
            return Falla(ErroresDeRetiro.VistaPreviaCambio(request.ExpectedPreviewSha256, visto.Value.Huella));
        var beneficiario = await ordenes.BeneficiarioAsync(leido.Value.PersonId, request.Refund.Payee?.BankPublicId, request.Refund.Payee?.AccountType,
            request.Refund.Payee?.AccountNumber, ct);
        if (beneficiario.IsFailure) return Falla(beneficiario.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(RetirosDeAsociado.ExpedienteDeDevolucion,
            RetirosDeAsociado.RaizDeLaDevolucion(request.WithdrawalPublicId)), AccionesDeSegregacion.Radicar, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        var liquidacion = Guid.NewGuid();

        var hecho = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var retiro = await db.MembershipWithdrawals.FirstAsync(w => w.PublicId == request.WithdrawalPublicId, ct);
            if (!retiro.AdmiteLiquidar) return Result.Failure<MembershipWithdrawal>(ErroresDeRetiro.NoAprobado(retiro.Status));
            var previo = visto.Value;
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(previo.Codeudores.Append(retiro.PersonId).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            if (previo.Creditos.Count > 0) await cerrojo.BloquearCreditosAsync(previo.Creditos.Keys.Order().ToList(), ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync(previo.Cuentas.Keys.Order().ToList(), ModoDeEspera.Esperar, ct);

            // US5 (T136; FR-076): los ahorros que ganan interés se liquidan a la fecha antes de cruzar y devolver (la vista previa ya los sumó).
            var idsDeAhorro = previo.Cuentas.Keys.Where(k => k != retiro.ContributionAccountId).ToList();
            if (interesesALaFecha is not null)
                foreach (var ahorro in await db.MemberAccounts.Where(a => idsDeAhorro.Contains(a.Id)).OrderBy(a => a.Id).ToListAsync(ct))
                {
                    var alDia = await interesesALaFecha.LiquidarAsync(ahorro, fecha, $"Liquidación de intereses del retiro {retiro.Number}", ct);
                    if (alDia.IsFailure) return Result.Failure<MembershipWithdrawal>(alDia.Error);
                }

            // Bajo el cerrojo, lo mismo que se vio: si algo cambió entre la vista previa y el cerrojo, nada se aplica.
            var armado = await planes.ArmarAsync(retiro, fecha, request.LossDeduction, ct);
            if (armado.IsFailure) return Result.Failure<MembershipWithdrawal>(armado.Error);
            var plan = armado.Value;
            if (!string.Equals(plan.Huella, request.ExpectedPreviewSha256, StringComparison.OrdinalIgnoreCase))
                return Result.Failure<MembershipWithdrawal>(ErroresDeRetiro.VistaPreviaCambio(request.ExpectedPreviewSha256, plan.Huella));
            var calculo = plan.Calculo;
            var deudor = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == retiro.PersonId).Select(p => p.PublicId).FirstAsync(ct);

            Domain.Entities.Lending.Deposits.Transactions.MemberAccountOperation? deLaPerdida = null;
            if (plan.Perdida is { } p && calculo.Perdida.Count > 0)
            {
                var perdida = await cruce.DescontarPerdidaAsync(retiro, calculo.Perdida,
                    new WithdrawalLossV1 { Year = p.Year, Proportion = p.Proportion, SessionItemPublicId = p.SessionItemPublicId }, fecha, ct);
                if (perdida.IsFailure) return Result.Failure<MembershipWithdrawal>(perdida.Error);
                deLaPerdida = perdida.Value;
            }
            var cruces = await cruce.AplicarAsync(retiro, liquidacion, calculo.Cruces, fecha, deudor, ct);
            if (cruces.IsFailure) return Result.Failure<MembershipWithdrawal>(cruces.Error);

            // D43 (2026-10-18): si el mínimo irreducible se perforaría, se retiene sólo la devolución de los aportes; los ahorros se devuelven ya.
            var retenida = plan.MinimoDeLaEntidad.WouldBreach;
            var aDevolverAhora = retenida ? calculo.Devolucion.Where(d => d.CuentaId != retiro.ContributionAccountId).ToList() : calculo.Devolucion;
            IReadOnlyList<DevolucionDeCuentaOrdenada> devoluciones = [];
            if (aDevolverAhora.Sum(d => d.Monto) > 0m)
            {
                var devueltas = await ordenes.DevolverRetiroAsync(retiro, aDevolverAhora, beneficiario.Value, hoy, plan.DevolverHasta, ct);
                if (devueltas.IsFailure) return Result.Failure<MembershipWithdrawal>(devueltas.Error);
                devoluciones = devueltas.Value;
            }

            retiro.Liquidar(usuario, reloj.UtcNow, calculo.Aportes, calculo.Ahorros, calculo.Cruzado, calculo.Perdidas, calculo.ADevolver, plan.DevolverHasta,
                retenida, cruces.Value.FirstOrDefault()?.Operacion.Id);
            // D45 (2026-10-18): qué pasa con lo que los créditos siguen debiendo; por defecto, pasan a caja.
            retiro.PendingLoanBalanceHandling = calculo.SaldoPendienteDeCreditos > 0m
                ? request.PendingLoanBalance ?? PendingLoanBalanceHandling.MoveToCashier
                : null;
            var numero = 0;
            foreach (var l in calculo.Lineas)
            {
                long? deCuenta = l.Clase switch
                {
                    MembershipWithdrawalLineKind.LossDeduction => deLaPerdida?.Id,
                    MembershipWithdrawalLineKind.LoanOffset => cruces.Value.FirstOrDefault(c => c.CuentaId == l.CuentaId && c.CreditoId == l.CreditoId)?.Operacion.Id,
                    MembershipWithdrawalLineKind.Refund => devoluciones.FirstOrDefault(d => d.CuentaId == l.CuentaId)?.Operacion.Id,
                    _ => null,
                };
                long? deCredito = l.Clase == MembershipWithdrawalLineKind.LoanOffset
                    ? cruces.Value.FirstOrDefault(c => c.CuentaId == l.CuentaId && c.CreditoId == l.CreditoId)?.OperacionDelCredito
                    : null;
                db.MembershipWithdrawalLines.Add(new MembershipWithdrawalLine
                {
                    MembershipWithdrawalId = retiro.Id,
                    SettlementVersion = retiro.SettlementVersion,
                    LineNumber = ++numero,
                    Kind = l.Clase,
                    MemberAccountId = l.CuentaId,
                    LoanId = l.CreditoId,
                    Concept = l.Concepto,
                    Amount = l.Monto,
                    AccountOperationId = deCuenta,
                    LoanOperationId = deCredito,
                    Notes = l.Clase == MembershipWithdrawalLineKind.Refund && retenida && l.CuentaId == retiro.ContributionAccountId
                        ? "Pendiente: perforaría el mínimo irreducible de la entidad"
                        : l.Nota,
                });
            }
            await db.SaveChangesAsync(ct);

            if (calculo.ADevolver == 0m)
            {
                retiro.Pagar(reloj.UtcNow);
                await ordenes.CerrarCuentasAsync(retiro, hoy, ct);
            }
            else if (devoluciones.Count > 0)
            {
                await PedirCierreAsync(devoluciones.Select(d => d.CuentaId).Distinct().ToList(), ct);
            }
            intervenciones.Registrar(verificada.Value, RetirosDeAsociado.ExpedienteDeDevolucion, retiro.PublicId);
            await db.SaveChangesAsync(ct);
            if (retenida && alertas is not null)
                await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.DevolucionRetenidaPorMinimo,
                    $"Devolución del retiro {retiro.Number} retenida",
                    $"La devolución de los aportes del retiro {retiro.Number} ({calculo.DevolucionDeAportes([retiro.ContributionAccountId]):N2}) dejaría el capital "
                    + $"social en {plan.MinimoDeLaEntidad.CapitalAfter:N2}, por debajo del mínimo irreducible de la entidad ({plan.MinimoDeLaEntidad.Minimum:N2}). "
                    + "El retiro quedó liquidado y los ahorros se devolvieron; la devolución de los aportes se ordena con «Aprobar devolución» cuando el capital "
                    + "lo permita.",
                    nameof(MembershipWithdrawal), retiro.PublicId, DedupKey: $"{TiposDeAlerta.DevolucionRetenidaPorMinimo}:{retiro.PublicId:N}",
                    RecipientPermissions: ["Lending.MembershipWithdrawals.Approve"], ModuloDeOrigen: "Lending"), ct);
            return Result.Success(retiro);
        }, ct);
        if (hecho.IsFailure) return Falla(hecho.Error);

        // D45 (2026-10-18): los créditos que siguen debiendo pasan a caja si así se eligió: las libranzas y la nómina pasan a la forma de pago alterna.
        if (hecho.Value.PendingLoanBalanceHandling == PendingLoanBalanceHandling.MoveToCashier)
            await PasarACajaAsync(hecho.Value, ct);

        // La liquidación emitida (documentos.md §2.8): nace al liquidar, después de confirmar. Si no se puede emitir (sin firmante), el retiro queda
        // liquidado igual y se emite después desde Documentos.
        try
        {
            var documento = await sender.Send(new IssueLendingDocumentCommand(IssuedDocumentKind.MembershipWithdrawalSettlement,
                await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == hecho.Value.PersonId).Select(p => p.PublicId).FirstAsync(ct),
                null, null, null, null, DocumentDeliveryMode.Download)
            {
                MembershipWithdrawalPublicId = hecho.Value.PublicId,
                OperationKey = Guid.NewGuid(),
            }, ct);
            if (documento.IsFailure)
                logger.LogWarning("La liquidación del retiro {Retiro} no se emitió: {Codigo} {Mensaje}", hecho.Value.Number, documento.Error.Code, documento.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "La liquidación del retiro {Retiro} no se emitió.", hecho.Value.Number);
        }
        db.DescartarCambios();
        var final = await db.MembershipWithdrawals.AsNoTracking().FirstAsync(w => w.Id == hecho.Value.Id, ct);
        return Result.Success(await retiros.DtoAsync(final, conLineas: true, ct));
    }

    /// <summary>
    /// D45: los créditos vivos del exasociado con una libranza o un descuento de nómina vigente pasan a recibir pagos por caja, por el traslado de la
    /// libranza sin pagaduría nueva de la 013 (la forma alterna del convenio). Después de confirmar la liquidación: si un traslado no se puede, el
    /// retiro queda liquidado y se registra en el log.
    /// </summary>
    private async Task PasarACajaAsync(MembershipWithdrawal retiro, CancellationToken ct)
    {
        var creditos = await db.Loans.AsNoTracking()
            .Where(l => l.DebtorPersonId == retiro.PersonId && !l.IsDeleted && l.Status == LoanStatus.Active)
            .Select(l => new { l.Id, l.PublicId, l.Number }).ToListAsync(ct);
        var ids = creditos.Select(c => c.Id).ToList();
        var conDescuento = await db.LoanPayrollLinks.AsNoTracking()
            .Where(v => ids.Contains(v.LoanId) && v.ValidTo == null && !v.IsDeleted
                        && (v.RepaymentChannel == RepaymentChannel.OwnPayroll || v.RepaymentChannel == RepaymentChannel.PaymasterDeduction))
            .Select(v => v.LoanId).Distinct().ToListAsync(ct);
        foreach (var credito in creditos.Where(c => conDescuento.Contains(c.Id)))
        {
            try
            {
                var r = await sender.Send(new Payroll.Transfers.TransferLoanPayrollCommand(credito.PublicId,
                    $"Retiro del asociado {retiro.Number}: el saldo pendiente pasa a caja (D45)") { OperationKey = Guid.NewGuid() }, ct);
                if (r.IsFailure)
                    logger.LogWarning("El crédito {Credito} del retiro {Retiro} no pasó a caja: {Codigo} {Mensaje}", credito.Number, retiro.Number, r.Error.Code,
                        r.Error.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "El crédito {Credito} del retiro {Retiro} no pasó a caja.", credito.Number, retiro.Number);
            }
        }
    }

    /// <summary>Las cuentas que se devuelven quedan en cierre hasta que la orden se gire (FR-076).</summary>
    private async Task PedirCierreAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        foreach (var cuenta in await db.MemberAccounts.Where(a => ids.Contains(a.Id)).ToListAsync(ct))
            if (cuenta.Status != MemberAccountStatus.Closing && MemberAccount.PuedePasar(cuenta.Status, MemberAccountStatus.Closing))
                cuenta.PedirCierre(ahora);
    }

    private static Result<MembershipWithdrawalDto> Falla(Error e) => Result.Failure<MembershipWithdrawalDto>(e);
}

public sealed class ApproveMembershipRefundCommandHandler(
    IApplicationDbContext db,
    RetirosDeAsociado retiros,
    IAlcanceDeCartera alcance,
    ReglasDeAportes reglas,
    OrdenesDeGiroDeCuentas ordenes,
    RegistroDeIntervenciones intervenciones,
    ICerrojoDeCartera cerrojo,
    IDateTimeService reloj) : IRequestHandler<ApproveMembershipRefundCommand, Result<MembershipWithdrawalDto>>
{
    public async Task<Result<MembershipWithdrawalDto>> Handle(ApproveMembershipRefundCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leido = await retiros.EnAlcanceAsync(alcance, request.WithdrawalPublicId, seguir: false, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        if (leido.Value.Status != MembershipWithdrawalStatus.Settled) return Falla(ErroresDeRetiro.NoLiquidado(leido.Value.Status));
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(RetirosDeAsociado.ExpedienteDeDevolucion,
            RetirosDeAsociado.RaizDeLaDevolucion(request.WithdrawalPublicId)), AccionesDeSegregacion.Decidir, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        var beneficiario = await ordenes.BeneficiarioAsync(leido.Value.PersonId, request.Payee?.BankPublicId, request.Payee?.AccountType,
            request.Payee?.AccountNumber, ct);
        if (beneficiario.IsFailure) return Falla(beneficiario.Error);

        var hecho = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var retiro = await db.MembershipWithdrawals.FirstAsync(w => w.PublicId == request.WithdrawalPublicId, ct);
            if (retiro.Status != MembershipWithdrawalStatus.Settled) return Result.Failure<MembershipWithdrawal>(ErroresDeRetiro.NoLiquidado(retiro.Status));
            var cuentas = await ordenes.CuentasDelRetiroAsync(retiro, ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([retiro.PersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync(cuentas.Select(c => c.Id).Order().ToList(), ModoDeEspera.Esperar, ct);

            var conOrdenViva = await db.AccountPayoutOrders.AsNoTracking()
                .Where(o => o.MembershipWithdrawalId == retiro.Id && !o.IsDeleted && o.Status == DisbursementOrderStatus.Issued)
                .Select(o => o.MemberAccountId).ToListAsync(ct);
            var saldos = await LectorDeCuentas.SaldosAsync(db, cuentas.Select(c => c.Id).ToList(), ct);
            var pendiente = cuentas.Where(c => !conOrdenViva.Contains(c.Id))
                .SelectMany(c => (saldos.GetValueOrDefault(c.Id) ?? []).Where(s => s.Value > 0m && Domain.Entities.Lending.Deposits.Transactions.MemberAccountTransaction
                    .ConceptosDelTitular.Contains(s.Key)).OrderBy(s => (int)s.Key).Select(s => new DebitoDeCuentaEnElRetiro(c.Id, s.Key, s.Value)))
                .ToList();
            if (pendiente.Count == 0) return Result.Failure<MembershipWithdrawal>(ErroresDeRetiro.NadaQueDevolver());

            // FR-080: el mínimo irreducible de la entidad se vuelve a comparar con lo que sale de aportes.
            if (await reglas.MinimoDeLaEntidadAsync(hoy, ct) is { } minimo and > 0m)
            {
                var capital = await reglas.CapitalSocialAsync(ct);
                var deAportes = pendiente.Where(d => d.CuentaId == retiro.ContributionAccountId).Sum(d => d.Monto);
                if (capital - deAportes < minimo) return Result.Failure<MembershipWithdrawal>(ErroresDeAportes.MinimoDeLaEntidad(minimo, capital));
            }
            var devueltas = await ordenes.DevolverRetiroAsync(retiro, pendiente, beneficiario.Value, hoy, retiro.RefundDueOn ?? hoy, ct);
            if (devueltas.IsFailure) return Result.Failure<MembershipWithdrawal>(devueltas.Error);
            retiro.MarcarRetencionPorMinimo(false);
            var ahora = reloj.UtcNow;
            foreach (var cuenta in cuentas.Where(c => devueltas.Value.Any(d => d.CuentaId == c.Id)))
                if (cuenta.Status != MemberAccountStatus.Closing && MemberAccount.PuedePasar(cuenta.Status, MemberAccountStatus.Closing))
                    cuenta.PedirCierre(ahora);
            intervenciones.Registrar(verificada.Value, RetirosDeAsociado.ExpedienteDeDevolucion, retiro.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(retiro);
        }, ct);
        if (hecho.IsFailure) return Falla(hecho.Error);
        return Result.Success(await retiros.DtoAsync(hecho.Value, conLineas: true, ct));
    }

    private static Result<MembershipWithdrawalDto> Falla(Error e) => Result.Failure<MembershipWithdrawalDto>(e);
}
