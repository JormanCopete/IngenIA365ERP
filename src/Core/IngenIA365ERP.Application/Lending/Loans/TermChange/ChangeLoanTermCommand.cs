using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Schedules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans.TermChange;

/// <summary>Una cuota del plan nuevo en la simulación del cambio de plazo. (nuevo)</summary>
public sealed record TermChangeInstallmentDto(int Number, DateOnly DueDate, decimal Principal, decimal Interest, decimal Insurance, decimal Administration,
    decimal Total, decimal ClosingPrincipal);

/// <summary>
/// La simulación del cambio de plazo (2026-11-24): el plazo y la cuota de hoy, los nuevos, el plan nuevo completo, la usura (veredicto y efectiva con
/// cargos) y los avisos. (nuevo)
/// </summary>
public sealed record LoanTermChangeSimulationDto(
    Guid LoanPublicId,
    string LoanNumber,
    DateOnly ValueDate,
    int CurrentInstallments,
    decimal CurrentInstallmentAmount,
    DateOnly CurrentFirstDueDate,
    DateOnly CurrentLastDueDate,
    int NewInstallments,
    decimal NewInstallmentAmount,
    DateOnly NewFirstDueDate,
    DateOnly NewLastDueDate,
    decimal Principal,
    decimal NewTotalInterest,
    string UsuryVerdict,
    decimal EffectiveRateWithCharges,
    decimal? Usury,
    IReadOnlyList<TermChangeInstallmentDto> Schedule,
    IReadOnlyList<string> Warnings);

/// <summary>El cambio de plazo registrado: su operación, la versión nueva del plan y lo que quedó. (nuevo)</summary>
public sealed record LoanTermChangeResultDto(
    Guid OperationPublicId,
    string OperationNumber,
    int ScheduleVersion,
    int TermsVersion,
    int InstallmentsBefore,
    int InstallmentsAfter,
    decimal InstallmentAmountBefore,
    decimal InstallmentAmountAfter,
    DateOnly FirstDueDate,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Simula el cambio de plazo de un crédito sin pagos ni causación (2026-11-24, pedido del dueño; <c>GET /api/lending/loans/{id}/term-change/simulation</c>,
/// <c>Lending.Loans.ChangeTerm</c>): las mismas reglas y el mismo cálculo que el registro (<see cref="CambioDePlazo"/>), sin escribir. Si el crédito no
/// lo admite, el error lo dice. (nuevo)
/// </summary>
public sealed record SimulateLoanTermChangeQuery(Guid LoanPublicId, int Installments, DateOnly? FirstDueDate = null)
    : IRequest<Result<LoanTermChangeSimulationDto>>;

public sealed class SimulateLoanTermChangeQueryValidator : AbstractValidator<SimulateLoanTermChangeQuery>
{
    public SimulateLoanTermChangeQueryValidator()
    {
        RuleFor(x => x.LoanPublicId).NotEmpty();
        RuleFor(x => x.Installments).InclusiveBetween(1, 600).WithMessage("El número de cuotas va de 1 a 600.");
    }
}

public sealed class SimulateLoanTermChangeQueryHandler(
    IApplicationDbContext db,
    CambioDePlazo cambio,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria) : IRequestHandler<SimulateLoanTermChangeQuery, Result<LoanTermChangeSimulationDto>>
{
    public async Task<Result<LoanTermChangeSimulationDto>> Handle(SimulateLoanTermChangeQuery request, CancellationToken ct)
    {
        var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == request.LoanPublicId && !l.IsDeleted, ct);
        if (credito is null) return Result.Failure<LoanTermChangeSimulationDto>(ErroresDeDesembolso.CreditoInexistente());
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeDesembolso.CreditoInexistente(), auditoria, nameof(Loan),
            credito.PublicId, ct);
        if (dentro.IsFailure) return Result.Failure<LoanTermChangeSimulationDto>(dentro.Error);
        if (!await cambio.PermitidoAsync(credito, ct)) return Result.Failure<LoanTermChangeSimulationDto>(ErroresDelCambioDePlazo.Deshabilitado());

        var calculado = await cambio.CalcularAsync(credito, request.Installments, request.FirstDueDate, ct);
        return calculado.IsFailure ? Result.Failure<LoanTermChangeSimulationDto>(calculado.Error) : Result.Success(Dto(calculado.Value));
    }

    public static LoanTermChangeSimulationDto Dto(CambioDePlazoCalculado c) => new(
        c.Credito.PublicId, c.Credito.Number, c.FechaValor,
        c.PlanAntes.Cuotas.Count, CambioDePlazoCalculado.CuotaRegular(c.PlanAntes), c.PlanAntes.Cuotas[0].Vencimiento, c.PlanAntes.Cuotas[^1].Vencimiento,
        c.PlanNuevo.Cuotas.Count, CambioDePlazoCalculado.CuotaRegular(c.PlanNuevo), c.PlanNuevo.Cuotas[0].Vencimiento, c.PlanNuevo.Cuotas[^1].Vencimiento,
        c.PlanNuevo.TotalCapital, c.PlanNuevo.TotalInteres, c.Usura.Veredicto.ToString(), c.Usura.EfectivaConCargos, c.Usura.Usura,
        c.PlanNuevo.Cuotas.Select(q => new TermChangeInstallmentDto(q.Numero, q.Vencimiento, q.Capital, q.Interes, q.Seguro, q.Administracion, q.Total,
            q.SaldoFinal)).ToList(),
        c.Avisos);
}

/// <summary>
/// <b>Cambia el plazo</b> de un crédito que todavía no tiene pagos ni causación (2026-11-24, pedido del dueño; <c>POST /api/lending/loans/{id}/term-change</c>,
/// <c>Lending.Loans.ChangeTerm</c>, con clave y motivo). El caso típico: el crédito que nació hace minutos de una venta a crédito (la afiliación) con un
/// número de cuotas equivocado. No es una novedad —no se radica ni se decide, no es modificación ni reestructuración ni cuenta para el conteo— y no
/// mueve plata: el crédito queda como si hubiera nacido con las cuotas nuevas. En una transacción:
/// <list type="number">
/// <item>el parámetro <c>Creditos.CambioDePlazo</c> (de fábrica, sí; por cooperativa o línea) y, si la cooperativa lo pide
/// (<c>Segregacion.CambioDePlazoPorOtraPersona</c>), que no lo haga quien registró el crédito;</item>
/// <item>los cerrojos del deudor y del crédito y el cálculo de <see cref="CambioDePlazo"/> (sin pagos, sin causación, sin otras operaciones; rangos de
/// la línea; usura);</item>
/// <item>por <see cref="RegistroDeCartera"/>: la operación <c>TermsChange</c> (número <c>CPZ-</c>, fecha valor = la del desembolso, sin novedad, con la
/// decisión de mecanismo <see cref="NoveltyMechanism.TermCorrection"/> que lee la reconstrucción), la versión de condiciones nueva (causa
/// <see cref="TermsVersionCause.TermCorrection"/>) y la versión nueva del plan; el plan anterior queda como versión (Principio XI). No deja filas en el
/// libro —si las dejara, se rechaza (<c>Lending.Loan.TermChange.WouldChangeLedger</c>)—, así que no hay hecho contable ni mensaje a Comercial;</item>
/// <item>la posición del día con el plan nuevo y la auditoría de la operación (antes y después).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record ChangeLoanTermCommand(Guid LoanPublicId, int Installments, DateOnly? FirstDueDate, string Reason)
    : IRequest<Result<LoanTermChangeResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ChangeLoanTermCommandValidator : AbstractValidator<ChangeLoanTermCommand>
{
    public ChangeLoanTermCommandValidator()
    {
        RuleFor(x => x.LoanPublicId).NotEmpty();
        RuleFor(x => x.Installments).InclusiveBetween(1, 600).WithMessage("El número de cuotas va de 1 a 600.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se cambia el plazo.").MaximumLength(500);
    }
}

public sealed class ChangeLoanTermCommandHandler(
    IApplicationDbContext db,
    CambioDePlazo cambio,
    CargadorDeReconstruccion cargador,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ChangeLoanTermCommand, Result<LoanTermChangeResultDto>>
{
    public async Task<Result<LoanTermChangeResultDto>> Handle(ChangeLoanTermCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leido = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == request.LoanPublicId && !l.IsDeleted, ct);
        if (leido is null) return Falla(ErroresDeDesembolso.CreditoInexistente());
        var dentro = await alcance.AsegurarAsync(leido.BranchId, leido.CreditLineId, ErroresDeDesembolso.CreditoInexistente(), auditoria, nameof(Loan),
            leido.PublicId, ct);
        if (dentro.IsFailure) return Falla(dentro.Error);
        if (!await cambio.PermitidoAsync(leido, ct)) return Falla(ErroresDelCambioDePlazo.Deshabilitado());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([leido.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync([leido.Id], ModoDeEspera.Esperar, ct);

            var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == leido.Id, ct);
            var calculado = await cambio.CalcularAsync(credito, request.Installments, request.FirstDueDate, ct);
            if (calculado.IsFailure) return Falla(calculado.Error);
            var c = calculado.Value;
            if (await cambio.ExigeOtraPersonaAsync(ct) && await RegistroElCreditoAsync(c, usuario, ct)) return Falla(ErroresDelCambioDePlazo.MismaPersona());

            var control = await db.LoanControls.FirstAsync(x => x.LoanId == credito.Id, ct);
            var ahora = reloj.UtcNow;
            var payload = c.Decision.Serializar();
            var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.TermsChange, CambioDePlazo.PrefijoDeOperacion, ct);
            var motivo = request.Reason.Trim();
            var operacion = registro.RegistrarOperacion(control, ahora, sello => new LoanOperation
            {
                LoanId = credito.Id,
                Kind = LoanOperationKind.TermsChange,
                Number = numero,
                Sequence = sello.Sequence,
                ValueDate = c.FechaValor,
                AccountingDate = c.FechaContable,
                RegisteredAt = sello.RegisteredAt,
                Amount = 0m,
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                OriginUserCentralId = actor.CentralUserId,
                NoveltyId = null,
                Reason = Recortar($"Cambio de plazo de {c.PlanAntes.Cuotas.Count} a {c.PlanNuevo.Cuotas.Count} cuotas: {motivo}"),
                Payload = payload,
                InputsSha256 = TramoDeDesembolso.Sha256(payload),
                RulesVersion = c.PlanNuevo.RulesVersion,
                PrevalidationOutcome = PrevalidationOutcome.NotApplicable.ToString(),
            });

            // El efecto en el libro: un crédito sin pagos ni causación no debe tener ninguno. Si lo tuviera, no se cambia por aquí.
            var entrada = c.Cargado.Entrada!;
            var marca = control.LastAccruedDate;
            var libro = marca >= c.FechaValor ? (await cargador.LibroAsync([credito.Id], marca, ct))[credito.Id] : [];
            EfectoEnElLibro efecto;
            try
            {
                efecto = EfectoEnElLibro.Calcular(entrada, CargadorDeReconstruccion.ConOperacion(entrada, operacion, c.Contenido),
                    CargadorDeReconstruccion.IdEnLaEntrada(operacion), c.FechaValor, marca, libro, []);
            }
            catch (ReglaDeCarteraException ex)
            {
                return Falla(ErroresDeGobierno.De(ex.Problema));
            }
            if (efecto.Propias.Count > 0 || efecto.Diferencias.Count > 0 || efecto.Despues.Plan is null) return Falla(ErroresDelCambioDePlazo.MoveriaElLibro());
            var plan = efecto.Despues.Plan;

            var condiciones = CambioDePlazo.Version(c.Cargado.Vigentes, operacion, plan, c.FechaValor, c.Decision.PaymentDay);
            registro.RegistrarVersionDeCondiciones(condiciones);
            var versionDelPlan = (await db.RepaymentScheduleVersions.AsNoTracking().Where(p => p.LoanId == credito.Id)
                .MaxAsync(p => (int?)p.VersionNumber, ct) ?? 0) + 1;
            registro.RegistrarPlanRegenerado(credito.Id, versionDelPlan, condiciones, operacion, plan, entrada.Festivos);
            await db.SaveChangesAsync(ct);

            // La posición del último día con estado: la del plan nuevo (otra versión, nunca encima).
            var dia = efecto.Despues.Fecha;
            var ultimas = await PosicionesDeCartera.UltimasAsync(db, [credito.Id], dia, dia, ct);
            var u = ultimas.GetValueOrDefault((credito.Id, dia));
            registro.PosicionVersionada(control, efecto.Despues, credito, condiciones, u.Version, u.Huella, ahora);
            await db.SaveChangesAsync(ct);

            var antes = CambioDePlazoCalculado.CuotaRegular(c.PlanAntes);
            var despues = CambioDePlazoCalculado.CuotaRegular(plan);
            await auditoria.EmitirOperacionRegistradaAsync(operacion.PublicId, operacion.Number, nameof(LoanOperationKind.TermsChange), new
            {
                change = nameof(NoveltyMechanism.TermCorrection),
                reason = motivo,
                valueDate = c.FechaValor,
                installmentsBefore = c.PlanAntes.Cuotas.Count,
                installmentsAfter = plan.Cuotas.Count,
                installmentBefore = antes,
                installmentAfter = despues,
                firstDueDateBefore = c.PlanAntes.Cuotas[0].Vencimiento,
                firstDueDateAfter = plan.Cuotas[0].Vencimiento,
                scheduleVersion = versionDelPlan,
                schedule = plan.Cuotas.Select(q => new { installment = q.Numero, dueDate = q.Vencimiento, principal = q.Capital, interest = q.Interes, total = q.Total }),
            }, ct);

            return Result.Success(new LoanTermChangeResultDto(operacion.PublicId, operacion.Number, versionDelPlan, condiciones.VersionNumber,
                c.PlanAntes.Cuotas.Count, plan.Cuotas.Count, antes, despues, plan.Cuotas[0].Vencimiento, c.Avisos));
        }, ct);
    }

    /// <summary>
    /// ¿Registró el crédito (su desembolso o la venta a crédito) la misma persona que ahora cambia el plazo? Como toda la segregación de Cartera,
    /// compara <b>personas</b> —dos usuarios de la misma persona son la misma— y, si alguno no tiene persona, el usuario (<c>SEC_Users.Id</c>).
    /// </summary>
    private async Task<bool> RegistroElCreditoAsync(CambioDePlazoCalculado c, int usuario, CancellationToken ct)
    {
        var quienes = c.Cargado.Operaciones.Where(o => o.Kind == LoanOperationKind.Disbursement && o.ActorUserId is not null)
            .Select(o => o.ActorUserId!.Value).Distinct().ToList();
        if (quienes.Count == 0) return false;
        if (quienes.Contains(usuario)) return true;
        var personas = await db.Users.AsNoTracking().Where(u => (quienes.Contains(u.Id) || u.Id == usuario) && u.PersonId != null)
            .Select(u => new { u.Id, u.PersonId }).ToListAsync(ct);
        var mia = personas.FirstOrDefault(p => p.Id == usuario)?.PersonId;
        return mia is not null && personas.Any(p => p.Id != usuario && p.PersonId == mia);
    }

    private static string Recortar(string texto) => texto.Length <= 500 ? texto : texto[..500];

    private static Result<LoanTermChangeResultDto> Falla(Error e) => Result.Failure<LoanTermChangeResultDto>(e);
}
