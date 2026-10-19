using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Contractual;

// Feature 014, US7 (T182, T185; api.md §4): la instrucción al vencimiento del contractual, la vista del contrato y la historia de sus condiciones selladas.
// Las tres exigen el contractual en el alcance (404 de la cuenta fuera de él). (nuevo)

// ---------------------------------------------------------------------------------------------------------- instrucción --

/// <summary>
/// <c>PUT /api/lending/accounts/{id}/maturity-instruction</c> (<c>Lending.Accounts.Parties.Manage</c>, con clave; api.md §4; FR-075): la instrucción al
/// vencimiento del contractual —trasladar a una cuenta de ahorro a la vista viva del titular o girar—, mientras el contrato no haya terminado. Se guarda en la
/// cuenta (<c>MaturityInstructionJson</c>): la sellada es hecho y no se edita. Sólo un contractual (<c>Lending.Savings.NotContractual</c>). (nuevo)
/// </summary>
public sealed record SetContractualMaturityInstructionCommand(Guid AccountPublicId, ContractualMaturityInstructionInput Instruction, string Reason)
    : IRequest<Result<ContractualDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetContractualMaturityInstructionCommandValidator : ValidadorConMotivo<SetContractualMaturityInstructionCommand>
{
    public SetContractualMaturityInstructionCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Instruction).NotNull();
        RuleFor(x => x.Instruction.Kind).IsInEnum().When(x => x.Instruction is not null);
        RuleFor(x => x.Instruction.TargetAccountPublicId).NotEmpty()
            .When(x => x.Instruction is { Kind: CloseDeliveryKind.TransferToAccount }).WithMessage("El traslado nombra la cuenta de ahorro destino.");
    }
}

public sealed class SetContractualMaturityInstructionCommandHandler(
    IApplicationDbContext db,
    IContextoDeLineasDeCaptacion contexto,
    AperturaDeContractual apertura,
    LectorDeContractual lector,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<SetContractualMaturityInstructionCommand, Result<ContractualDto>>
{
    public async Task<Result<ContractualDto>> Handle(SetContractualMaturityInstructionCommand request, CancellationToken ct)
    {
        if (contexto.Entrega < EntregaDeCartera.K9) return Falla(ErroresDeContractual.EntregaNoDisponible("El ahorro contractual"));
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        if (cuenta.DepositClass != DepositClass.Contractual) return Falla(ErroresDeContractual.NoEsContractual(cuenta.Number));
        if (cuenta.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive or MemberAccountStatus.Opening)
            || cuenta.ContractualStatus == ContractualStatus.Fulfilled)
            return Falla(ErroresDeContractual.NoVigente(cuenta.Number, cuenta.Status, cuenta.ContractualStatus));
        var destino = await apertura.DestinoAsync(cuenta, request.Instruction, ct);
        if (destino.IsFailure) return Falla(destino.Error);

        var i = request.Instruction;
        var persona = i.Kind == CloseDeliveryKind.DisbursementOrder && destino.Value.PersonaId is { } p
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == p).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var banco = destino.Value.BancoId is { } b
            ? await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == b).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var actor = await actorActual.ObtenerAsync(ct);
        cuenta.MaturityInstructionJson = new InstruccionDeContractual(i.Kind, i.Kind == CloseDeliveryKind.TransferToAccount ? i.TargetAccountPublicId : null, persona, banco,
            destino.Value.TipoDeCuenta, destino.Value.NumeroDeCuenta, actor.UserId, reloj.UtcNow, IncumplimientoDeContractual.Recortar(request.Reason, 300)).Serializar();
        await db.SaveChangesAsync(ct);
        return await RespuestasDeContractual.DtoAsync(db, lector, cuenta, reloj.HoyLocal, ct);
    }

    private static Result<ContractualDto> Falla(Error e) => Result.Failure<ContractualDto>(e);
}

// ------------------------------------------------------------------------------------------------------------- consultas --

/// <summary>
/// El ahorro contractual (<c>GET /api/lending/accounts/{id}/contractual</c>, <c>Lending.Accounts.Read</c>; la pantalla <c>ContratoDeAhorro</c>): el contrato
/// sellado y su historia, las cuotas del plan con su estado, el cumplimiento, lo proyectado y la instrucción al vencimiento. Muestra los saldos del titular:
/// lectura sensible (<c>Lending.Savings.BalanceViewed</c>). (nuevo)
/// </summary>
public sealed record GetContractualQuery(Guid AccountPublicId) : IRequest<Result<ContractualDto>>;

public sealed class GetContractualQueryHandler(
    IApplicationDbContext db,
    LectorDeContractual lector,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetContractualQuery, Result<ContractualDto>>
{
    public async Task<Result<ContractualDto>> Handle(GetContractualQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<ContractualDto>(leida.Error);
        if (leida.Value.DepositClass != DepositClass.Contractual) return Result.Failure<ContractualDto>(ErroresDeContractual.NoEsContractual(leida.Value.Number));
        var dto = await RespuestasDeContractual.DtoAsync(db, lector, leida.Value, reloj.HoyLocal, ct);
        if (dto.IsSuccess) await auditoria.EmitirSaldoDeCuentaVistoAsync(leida.Value.PublicId, "contractual", new { date = reloj.HoyLocal }, ct);
        return dto;
    }
}

/// <summary>Una versión de condiciones selladas de una cuenta, la de un CDAT o la de un contractual (api.md §4, <c>GET /{id}/terms</c>). (nuevo)</summary>
public sealed record MemberAccountTermsVersionDto(
    int Version,
    AccountTermsCause Cause,
    DateOnly EffectiveDate,
    DateTime RegisteredAt,
    decimal PrincipalAmount,
    int? TermDays,
    int? TermMonths,
    DateOnly? StartDate,
    DateOnly? MaturityDate,
    decimal TableRate,
    decimal? NegotiatedSpread,
    decimal AppliedRate,
    IssueTermBand? IssueTermBand,
    decimal? Installment,
    RepaymentPeriodicity? InstallmentPeriodicity,
    int? Installments,
    decimal? BonusRate,
    decimal? BreachRate,
    string RulesVersion);

/// <summary>
/// Las condiciones selladas de una cuenta y su historia (<c>GET /api/lending/accounts/{id}/terms?version=</c>, <c>Lending.Accounts.Read</c>; api.md §4): la
/// versión pedida (la última si no se pide) y todas las anteriores, de un CDAT o un contractual. Sin condiciones selladas, lista vacía. (nuevo)
/// </summary>
public sealed record GetMemberAccountTermsQuery(Guid AccountPublicId, int? Version = null) : IRequest<Result<IReadOnlyList<MemberAccountTermsVersionDto>>>;

public sealed class GetMemberAccountTermsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetMemberAccountTermsQuery, Result<IReadOnlyList<MemberAccountTermsVersionDto>>>
{
    public async Task<Result<IReadOnlyList<MemberAccountTermsVersionDto>>> Handle(GetMemberAccountTermsQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<IReadOnlyList<MemberAccountTermsVersionDto>>(leida.Error);
        var versiones = await db.MemberAccountTermsVersions.AsNoTracking().Where(v => v.MemberAccountId == leida.Value.Id && !v.IsDeleted)
            .Where(v => request.Version == null || v.VersionNumber <= request.Version)
            .OrderByDescending(v => v.VersionNumber).ToListAsync(ct);
        return Result.Success<IReadOnlyList<MemberAccountTermsVersionDto>>(versiones.Select(v => new MemberAccountTermsVersionDto(v.VersionNumber, v.Cause,
            v.EffectiveValueDate, v.RegisteredAt, v.PrincipalAmount, v.TermDays, v.TermMonths, v.StartDate, v.MaturityDate, v.TableAnnualRate, v.NegotiatedSpread,
            v.AppliedAnnualRate, v.IssueTermBand, v.InstallmentAmount, v.InstallmentPeriodicity, v.InstallmentCount, v.BonusAnnualRate, v.BreachAnnualRate,
            v.RulesVersion)).ToList());
    }
}

/// <summary>
/// Las respuestas del contractual (feature 014, US7, T182, T185): el contrato con sus cuotas, el cumplimiento a la fecha, lo proyectado (los aportes con su
/// interés a la tasa base y, si la línea bonifica, la bonificación a la fecha; el valor al vencimiento si se cumple) y la instrucción que rige. Y el resumen de
/// la ficha de la cuenta (<see cref="ResumenAsync"/>). No escribe. (nuevo)
/// </summary>
public static class RespuestasDeContractual
{
    public static async Task<Result<ContractualDto>> DtoAsync(IApplicationDbContext db, LectorDeContractual lector, MemberAccount cuenta, DateOnly hoy,
        CancellationToken ct)
    {
        var leido = await lector.LeerAsync(cuenta, ct);
        if (leido.Inicial is not { } inicial || leido.Vigente is not { } vigente) return Result.Failure<ContractualDto>(ErroresDeContractual.SinCondiciones(cuenta.Number));
        var plan = LectorDeContractual.Plan(inicial);
        var cuotas = await lector.CuotasAsync(cuenta, plan, ct);
        var gracia = await lector.DiasDeGraciaAsync(cuenta.CreditLineId, hoy, ct);
        var cumplimiento = await lector.CumplimientoAsync(cuenta, inicial, plan, cuotas, hoy, ct);
        var c = LectorDeContractual.Condiciones(inicial);
        var corte = hoy < plan.Vencimiento ? hoy : plan.Vencimiento;
        var aportes = await lector.AportesAsync(leido, corte, ct);
        var interes = c.Redondeo.Redondear(AhorroContractual.Interes(aportes, c.TasaBase, c.Convencion, corte));
        DiferenciaDeTasasDelContractual? bonificacion = c.TasaBonificada is { } b
            ? AhorroContractual.Bonificacion(aportes, c.TasaBase, b, c.Convencion, corte, c.Redondeo)
            : null;
        var provisionada = await lector.BonificacionProvisionadaAsync(cuenta.Id, ct);
        var proyeccion = new ContractualProjectionDto(aportes.Sum(a => a.Monto), interes, bonificacion?.Diferencia, plan.ValorFinalBase,
            c.TasaBonificada is null ? null : plan.ValorFinalCumpliendo, plan.BonificacionProyectada, provisionada,
            bonificacion?.Explicacion ?? $"Aportes {aportes.Sum(a => a.Monto):N2} al {corte:yyyy-MM-dd} con su interés a la tasa base: {interes:N2}.");
        var lineasDeCuotas = cuotas.Select(q => new ContractualInstallmentDto(q.DelPlan.Numero, q.DelPlan.Vencimiento, q.Generada?.Cuota.Amount ?? c.Cuota,
            q.Pagado, q.Pendiente, q.Generada is null ? "NotGenerated" : q.Generada.Estado.ToString(),
            cumplimiento.NumerosAtrasados.Contains(q.DelPlan.Numero), q.Generada?.Cuota.PublicId)).ToList();
        return Result.Success(new ContractualDto(cuenta.PublicId, cuenta.Number, cuenta.Status, cuenta.ContractualStatus ?? ContractualStatus.Current,
            Terminos(inicial, vigente, plan), leido.Versiones.OrderByDescending(v => v.VersionNumber).Select(v => Terminos(inicial, v, plan)).ToList(),
            lineasDeCuotas,
            new ContractualComplianceDto(cumplimiento.Generadas, cumplimiento.Pagadas, cumplimiento.Atrasadas, cumplimiento.NumerosAtrasados, cumplimiento.ValorAtrasado,
                cumplimiento.Tolerancia, gracia, cumplimiento.SuperaLaTolerancia, cumplimiento.Completo),
            proyeccion, await InstruccionAsync(db, cuenta, vigente, ct), cuenta.MaturedOn));
    }

    /// <summary>El resumen del contrato en la ficha de la cuenta (api.md §4, <c>contractual</c>): las vencidas sin pagar son las de vencimiento anterior a hoy.</summary>
    public static async Task<ContractualSummaryDto?> ResumenAsync(IApplicationDbContext db, MemberAccount cuenta, DateOnly hoy, CancellationToken ct)
    {
        if (cuenta.DepositClass != DepositClass.Contractual) return null;
        var inicial = await db.MemberAccountTermsVersions.AsNoTracking().Where(v => v.MemberAccountId == cuenta.Id && !v.IsDeleted).OrderBy(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (inicial is null) return null;
        var cuotas = await Contributions.LectorDeCuotas.DeCuentasAsync(db, [cuenta.Id], ct);
        return new ContractualSummaryDto(inicial.InstallmentAmount ?? 0m, inicial.InstallmentPeriodicity ?? RepaymentPeriodicity.Monthly, inicial.InstallmentCount ?? 0,
            cuotas.Count(q => q.Pendiente == 0m), cuotas.Count(q => q.EnMoraEl(hoy)), cuenta.ContractualStatus ?? ContractualStatus.Current, inicial.MaturityDate);
    }

    private static ContractualTermsDto Terminos(MemberAccountTermsVersion inicial, MemberAccountTermsVersion v, PlanDeContractualCalculado plan) => new(
        v.VersionNumber, v.Cause, v.EffectiveValueDate, v.InstallmentAmount ?? 0m, v.InstallmentPeriodicity ?? RepaymentPeriodicity.Monthly,
        v.InstallmentCount ?? plan.Cuotas.Count, v.TermMonths ?? 0, v.StartDate ?? plan.Condiciones.Inicio, v.MaturityDate ?? plan.Vencimiento,
        inicial.AppliedAnnualRate, v.AppliedAnnualRate, v.BonusAnnualRate, v.BreachAnnualRate, v.ToleratedLateInstallments, v.BreachEffect, v.BonusRecognition,
        v.IssueTermBand, v.PrincipalAmount, v.ProjectedFinalAmount, v.MigratedPaidInstallments, v.RulesVersion);

    private static async Task<ContractualMaturityInstructionDto?> InstruccionAsync(IApplicationDbContext db, MemberAccount cuenta, MemberAccountTermsVersion v,
        CancellationToken ct)
    {
        var pedida = InstruccionDeContractual.Leer(cuenta.MaturityInstructionJson);
        if (pedida is not null)
        {
            var numero = pedida.TargetAccountPublicId is { } id
                ? await db.MemberAccounts.AsNoTracking().Where(a => a.PublicId == id).Select(a => a.Number).FirstOrDefaultAsync(ct)
                : null;
            var persona = pedida.PayeePersonPublicId is { } p ? await db.People.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(x => x.PublicId == p, ct) : null;
            return new ContractualMaturityInstructionDto(pedida.Kind, pedida.TargetAccountPublicId, numero, persona is null ? null : LectorDeCuentas.Persona(persona),
                pedida.Motivo, pedida.Dada);
        }
        if (v.DestinationAccountId is { } cuentaId)
        {
            var otra = await db.MemberAccounts.AsNoTracking().Where(a => a.Id == cuentaId).Select(a => new { a.PublicId, a.Number }).FirstOrDefaultAsync(ct);
            return new ContractualMaturityInstructionDto(CloseDeliveryKind.TransferToAccount, otra?.PublicId, otra?.Number, null, null, null);
        }
        if (v.MaturityInstruction == MaturityInstruction.CancelAndPay && v.DestinationPersonId is { } personaId)
        {
            var persona = await db.People.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == personaId, ct);
            return new ContractualMaturityInstructionDto(CloseDeliveryKind.DisbursementOrder, null, null, persona is null ? null : LectorDeCuentas.Persona(persona),
                null, null);
        }
        return null;
    }
}
