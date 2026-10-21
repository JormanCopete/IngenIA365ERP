using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// El nuevo giro de un crédito pendiente de giro (feature 013, US3, T0446; FR-048; api.md §9.2, <c>POST /api/lending/disbursement-orders/{id}/reissue</c>,
/// <c>Lending.Disbursements.Execute</c>, con clave): sólo sobre la última orden fallida (<c>Rejected</c>, <c>CheckNotCashed</c> o <c>Voided</c>) de un
/// crédito <c>PendingDisbursement</c> (<c>Lending.DisbursementOrder.NotReissuable</c>). Crea otro <c>Disbursement</c> del mismo tramo (con
/// <c>RelatedOperationId</c> = el reversado) y otra orden con <c>PreviousOrderId</c>, por el mismo bruto y lo mismo recogido —las recogidas se
/// conservaron (C11)—, con la forma y el beneficiario nuevos; el crédito vuelve a <c>Active</c>; emite <c>CreditoDesembolsado</c> y
/// <c>OrdenDeGiroEmitida</c> con <c>ReplacesOrderPublicId</c> y <c>related</c> = la anterior. El plan no se regenera aquí: la regeneración por la
/// fecha valor nueva la suma la fase 8 (<c>GeneradorDePlan</c>, D12). (nuevo)
/// </summary>
public sealed record ReissueDisbursementOrderCommand(Guid OrderPublicId, DisbursementForm Form, BeneficiaryInput? Beneficiary, string Reason, bool PaidByFile = false)
    : IRequest<Result<DisbursementOrderDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReissueDisbursementOrderCommandValidator : AbstractValidator<ReissueDisbursementOrderCommand>
{
    public ReissueDisbursementOrderCommandValidator()
    {
        RuleFor(x => x.OrderPublicId).NotEmpty();
        RuleFor(x => x.Form).IsInEnum().NotEqual(DisbursementForm.InKind);
        RuleFor(x => x.Beneficiary).NotNull().When(x => x.Form == DisbursementForm.ThirdPartyPayment);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se gira de nuevo.").MaximumLength(500);
    }
}

public sealed class ReissueDisbursementOrderCommandHandler(
    IApplicationDbContext db,
    TramoDeDesembolso tramos,
    ICerrojoDeCartera cerrojo,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    RegistroDeIntervenciones intervenciones,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ReissueDisbursementOrderCommand, Result<DisbursementOrderDto>>
{
    private static readonly DisbursementOrderStatus[] Fallidas =
        [DisbursementOrderStatus.Rejected, DisbursementOrderStatus.CheckNotCashed, DisbursementOrderStatus.Voided];

    public async Task<Result<DisbursementOrderDto>> Handle(ReissueDisbursementOrderCommand request, CancellationToken ct)
    {
        var leida = await OrdenesEnAlcance.LeerAsync(db, alcance, auditoria, request.OrderPublicId, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (anterior, credito) = leida.Value;
        if (credito.Status != LoanStatus.PendingDisbursement || !Fallidas.Contains(anterior.Status)
            || await db.DisbursementOrders.AnyAsync(o => o.PreviousOrderId == anterior.Id, ct))
            return Falla(ErroresDeDesembolso.OrdenNoReemitible(credito.Status.ToString()));

        var hoy = reloj.HoyLocal;
        var verificada = await intervenciones.VerificarAsync(await OrdenesEnAlcance.ExpedienteAsync(db, credito, ct), AccionesDeSegregacion.Desembolsar, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        var original = await db.LoanOperations.AsNoTracking().FirstAsync(o => o.Id == anterior.DisbursementOperationId, ct);
        if (await LimitesDePermisoPorUsuario.DeAsync(db, verificada.Value.UsuarioId, RequisitosDelDesembolso.PermisoDeDesembolsar, hoy, ct) is { } maximo
            && original.Amount > maximo)
            return Falla(ErroresDeDesembolso.MontoOperativo(maximo, original.Amount));

        var deudor = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == credito.DebtorPersonId, ct);
        var beneficiario = await BeneficiarioAsync(request, deudor, ct);
        if (beneficiario.IsFailure) return Falla(beneficiario.Error);
        var sucursal = await emision.SucursalContableAsync(credito.BranchId, ct);
        if (sucursal.IsFailure) return Falla(sucursal.Error);
        var fecha = await fechaContable.CalcularAsync(hoy, ct);
        if (fecha.IsFailure) return Falla(fecha.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync([credito.Id], ModoDeEspera.Esperar, ct);
            var c = await db.Loans.FirstAsync(l => l.Id == credito.Id, ct);
            if (c.Status != LoanStatus.PendingDisbursement) return Falla(ErroresDeDesembolso.OrdenNoReemitible(c.Status.ToString()));
            var control = await db.LoanControls.FirstAsync(x => x.LoanId == c.Id, ct);
            var condiciones = await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == c.Id).OrderByDescending(t => t.VersionNumber).FirstAsync(ct);

            var tramo = original.TrancheNumber ?? 1;
            var entradas = new { reissues = anterior.PublicId, reversed = original.PublicId, tranche = tramo, form = request.Form.ToString(),
                payee = beneficiario.Value.PersonPublicId, reason = request.Reason.Trim() };
            var operacion = await tramos.RegistrarAsync(c, control, tramo, request.Form, original.Amount, hoy, fecha.Value, entradas, condiciones.RulesVersion,
                PrevalidationOutcome.NotApplicable, original.Id, ct);
            RegistroDeCartera.CambiarEstado(c, LoanStatus.Active, hoy);
            await db.SaveChangesAsync(ct);

            var orden = await tramos.EmitirOrdenAsync(c, operacion, beneficiario.Value, original.Amount, anterior.PayoffAmount,
                request.PaidByFile && request.Form == DisbursementForm.Transfer, anterior.Id, ct);
            intervenciones.Registrar(verificada.Value, EmisionDeCartera.ClaseDesembolso, operacion.PublicId);

            var linea = await db.CreditLines.AsNoTracking().Where(l => l.Id == c.CreditLineId).Select(l => l.Code).FirstAsync(ct);
            var hecho = new HechoDeCredito(c, condiciones, linea, deudor.PublicId, deudor.IsEmployee, sucursal.Value, fecha.Value);
            var plan = await db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == c.Id).OrderByDescending(v => v.VersionNumber).FirstAsync(ct);
            var resumen = new LoanPlanSummaryV1
            {
                ScheduleVersion = plan.VersionNumber,
                AmortizationSystem = condiciones.AmortizationSystem.ToString(),
                Periodicity = condiciones.Periodicity.ToString(),
                InstallmentCount = plan.InstallmentCount,
                FirstDueDate = condiciones.FirstDueDate,
                AnnualEffectiveRate = condiciones.AnnualRate ?? 0m,
                DayCountConvention = condiciones.DayCount.ToString(),
                RulesVersion = plan.RulesVersion,
            };
            var emitidos = await emision.DesembolsoAsync(new DesembolsoAEmitir(hecho, operacion, tramo, true, request.Form, beneficiario.Value.ComoPayee(),
                original.Amount, [], orden, resumen, PrevalidationOutcome.NotApplicable, anterior.PublicId, anterior.Number), ct);
            if (emitidos.IsFailure) return Falla(emitidos.Error);
            await db.SaveChangesAsync(ct);
            return Result.Success((await ConsultasDeOrdenesDeGiro.DtosAsync(db, [orden], cuentaCompleta: true, ct))[0]);
        }, ct);
    }

    /// <summary>El beneficiario del giro nuevo: el pedido (o el deudor) con la cuenta de la entrada; en transferencia la cuenta es obligatoria.</summary>
    private async Task<Result<BeneficiarioResuelto>> BeneficiarioAsync(ReissueDisbursementOrderCommand request, Domain.Entities.Core.Person deudor, CancellationToken ct)
    {
        var persona = deudor;
        if (request.Beneficiary is { } b && b.PersonPublicId != deudor.PublicId)
        {
            var otra = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == b.PersonPublicId && !p.IsDeleted, ct);
            if (otra is null) return Result.Failure<BeneficiarioResuelto>(new Error(ErroresDeGobierno.CodigoPersonaInexistente, "El beneficiario no existe en el maestro de personas."));
            persona = otra;
        }
        var banco = request.Beneficiary?.BankPublicId is { } id
            ? await db.Banks.AsNoTracking().Where(x => x.PublicId == id && !x.IsDeleted).Select(x => new { x.Id, x.TransferCode }).FirstOrDefaultAsync(ct)
            : null;
        var tipo = BeneficiaryInput.Tipo(request.Beneficiary?.AccountType);
        var cuenta = request.Beneficiary?.AccountNumber?.Trim();
        if (request.Form == DisbursementForm.Transfer && (banco is null || tipo is null || string.IsNullOrWhiteSpace(cuenta)))
            return Result.Failure<BeneficiarioResuelto>(ErroresDeDesembolso.SinCuentaDelBeneficiario());
        var transferencia = request.Form == DisbursementForm.Transfer;
        return Result.Success(new BeneficiarioResuelto(persona.Id, persona.PublicId, persona.IdType, persona.TaxId, ReferenciasDeGobierno.Nombre(persona),
            transferencia ? banco?.Id : null, transferencia ? banco?.TransferCode : null, transferencia ? tipo : null, transferencia ? cuenta : null));
    }

    private static Result<DisbursementOrderDto> Falla(Error e) => Result.Failure<DisbursementOrderDto>(e);
}
