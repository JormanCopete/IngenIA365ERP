using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>Un crédito anulado y los reversos de sus recogidas. (nuevo)</summary>
public sealed record VoidedLoanDto(Guid LoanPublicId, string Number, string Status, DateOnly VoidedOn, string VoidReason, IReadOnlyList<Guid> PayoffReversalOperationPublicIds);

/// <summary>
/// Anula un crédito pendiente de giro (feature 013, US3, T0448; FR-048; api.md §8.4, <c>POST /api/lending/loans/{id}/void</c>,
/// <c>Lending.Disbursements.Void</c>, con clave y motivo): sólo <c>PendingDisbursement</c> sin otro tramo vivo
/// (<c>Lending.Loan.NotPendingDisbursement</c>). En la misma transacción reversa cada pago de recogida de sus desembolsos por
/// <see cref="IContratoDeObligaciones.ReversarAsync"/> (recaudador <c>CAR</c>, clave <c>{clave CAR}:reverso</c>; el contrato emite
/// <c>PagoReversado</c> y nunca <c>PagoReversadoPorCartera</c>) y los recogidos vuelven a su estado (C11; interfaces.md §4.3); el crédito queda
/// <c>Voided</c> con <c>VoidedOn</c> y <c>VoidReason</c>. Un reverso de recogida rechazado aborta todo. 2026-11-27 (feature 028, T040; FR-018): cada
/// recogida reversada marca su fila <c>LND_DisbursementPayoffs</c> con el instante y la clave del reverso. (nuevo)
/// </summary>
public sealed record VoidPendingLoanCommand(Guid LoanPublicId, string Reason) : IRequest<Result<VoidedLoanDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidPendingLoanCommandValidator : AbstractValidator<VoidPendingLoanCommand>
{
    public VoidPendingLoanCommandValidator()
    {
        RuleFor(x => x.LoanPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se anula el crédito.").MaximumLength(500);
    }
}

public sealed class VoidPendingLoanCommandHandler(
    IApplicationDbContext db,
    IContratoDeObligaciones contrato,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj,
    Payoffs.RecogidaDeCuentas? recogidaDeCuentas = null) : IRequestHandler<VoidPendingLoanCommand, Result<VoidedLoanDto>>
{
    public async Task<Result<VoidedLoanDto>> Handle(VoidPendingLoanCommand request, CancellationToken ct)
    {
        var leido = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == request.LoanPublicId && !l.IsDeleted, ct);
        if (leido is null) return Falla(ErroresDeDesembolso.CreditoInexistente());
        var dentro = await alcance.AsegurarAsync(leido.BranchId, leido.CreditLineId, ErroresDeDesembolso.CreditoInexistente(), auditoria, nameof(Loan), leido.PublicId, ct);
        if (dentro.IsFailure) return Falla(dentro.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var desembolsos = await db.LoanOperations.AsNoTracking()
                .Where(o => o.LoanId == leido.Id && o.Kind == LoanOperationKind.Disbursement)
                .Select(o => new { o.PublicId, o.ReversedByOperationId })
                .ToListAsync(ct);
            // Las recogidas viven en los créditos recogidos, con la clave CAR del desembolso: se bloquean con él, en una sentencia.
            var prefijos = desembolsos.Select(d => $"{ClavesDeRecaudador.Cartera}:{d.PublicId:N}:").ToList();
            var recogidas = (await db.LoanOperations.AsNoTracking()
                    .Where(o => o.CollectorCode == ClavesDeRecaudador.Cartera && o.Kind == LoanOperationKind.Payment && o.ReversedByOperationId == null
                                && o.IdempotencyKey != null)
                    .Select(o => new { o.LoanId, o.IdempotencyKey })
                    .ToListAsync(ct))
                .Where(o => prefijos.Any(p => o.IdempotencyKey!.StartsWith(p, StringComparison.Ordinal)))
                .ToList();

            // 2026-11-27 (feature 028, E3, T079): las consignaciones de recogida en cuentas de aportes y ahorros, con la clave CAR del desembolso.
            var consignaciones = await Payoffs.RecogidaDeCuentas.VivasAsync(db, desembolsos.Select(d => d.PublicId).ToList(), ct);

            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([leido.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync(recogidas.Select(r => r.LoanId).Append(leido.Id).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            // Las cuentas después de los créditos (el orden fijo, research E-14).
            if (consignaciones.Count > 0)
                await cerrojo.BloquearCuentasAsync(consignaciones.Select(c => c.CuentaId).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);

            var credito = await db.Loans.FirstAsync(l => l.Id == leido.Id, ct);
            if (credito.Status != LoanStatus.PendingDisbursement || desembolsos.Any(d => d.ReversedByOperationId is null))
                return Falla(ErroresDeDesembolso.CreditoNoPendiente(credito.Status.ToString()));

            var reversos = new List<Guid>();
            foreach (var r in recogidas)
            {
                var respuesta = await contrato.ReversarAsync(
                    new SolicitudDeReverso(RecogidaEnElDesembolso.Recaudador, r.IdempotencyKey!, request.Reason.Trim()), ct);
                if (respuesta.Result is not (ResultadoDelContrato.Applied or ResultadoDelContrato.AlreadyApplied) || respuesta.Value is null)
                    return Falla(respuesta.Data is { Count: > 0 } datos
                        ? new ErrorConDatos(respuesta.Code, respuesta.Message, datos)
                        : new Error(respuesta.Code, respuesta.Message));
                reversos.Add(respuesta.Value.OperationPublicId);
            }
            // 2026-11-27 (feature 028, E3, T079): cada consignación de recogida se reversa por ReversionDeCuentas con {clave}:reverso; la primera que no se
            // reversa aborta todo.
            if (consignaciones.Count > 0)
            {
                if (recogidaDeCuentas is null) return Falla(ErroresDeRecogida.ClaseNoAdmitida(PayoffProductKind.Account));
                var deCuentas = await recogidaDeCuentas.ReversarAsync(consignaciones, request.Reason.Trim(), ct);
                if (deCuentas.IsFailure) return Falla(deCuentas.Error);
                reversos.AddRange(deCuentas.Value.Select(x => x.Reverso));
            }

            // 2026-11-27 (feature 028, T040): la fila de cada recogida reversada queda marcada con la clave de su reverso (las de antes de la 028 no la tienen).
            var claves = recogidas.Select(r => r.IdempotencyKey!).Concat(consignaciones.Select(c => c.Clave)).ToList();
            if (claves.Count > 0)
                foreach (var fila in await db.DisbursementPayoffs.Where(f => claves.Contains(f.IdempotencyKey) && !f.Omitted).ToListAsync(ct))
                    fila.Reversar(reloj.UtcNow, ClavesDeRecaudador.Reverso(fila.IdempotencyKey));

            var hoy = reloj.HoyLocal;
            RegistroDeCartera.CambiarEstado(credito, LoanStatus.Voided, hoy, request.Reason);
            await db.SaveChangesAsync(ct);
            return Result.Success(new VoidedLoanDto(credito.PublicId, credito.Number, credito.Status.ToString(), hoy, credito.VoidReason!, reversos));
        }, ct);
    }

    private static Result<VoidedLoanDto> Falla(Error e) => Result.Failure<VoidedLoanDto>(e);
}
