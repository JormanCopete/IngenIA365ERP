using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payments.Commands;

/// <summary>
/// <c>POST /api/lending/loans/{id}/prepayment-preference</c> (feature 013, US5, T0554; api.md §8.3; FR-035, FR-075, D24;
/// <c>Lending.PrepaymentPreferences.Manage</c>, con clave): el deudor cambia (o revoca, con <see cref="Choice"/> nulo) su instrucción permanente para
/// imputar los abonos extraordinarios de este crédito. La del cupo rotativo es <c>CreditLimits.ChangePrepaymentPreferenceCommand</c> (US2). (nuevo)
/// </summary>
public sealed record ChangePrepaymentPreferenceCommand(Guid LoanPublicId, PrepaymentChoice? Choice, string Reason, Guid? EvidenceAttachmentPublicId = null)
    : IRequest<Result<PrepaymentPreferenceDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ChangePrepaymentPreferenceCommandValidator : AbstractValidator<ChangePrepaymentPreferenceCommand>
{
    public ChangePrepaymentPreferenceCommandValidator()
    {
        RuleFor(x => x.LoanPublicId).NotEmpty();
        RuleFor(x => x.Choice).IsInEnum().When(x => x.Choice is not null);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué cambia la preferencia (la instrucción del deudor).").MaximumLength(500);
    }
}

/// <summary>
/// En una <see cref="TransaccionExplicita"/> (T0554): el permiso <c>Lending.PrepaymentPreferences.Manage</c> (sin él, el 404 genérico, C85), el crédito
/// en el alcance y <c>Active</c> (<c>Lending.Loan.NotActive</c>), el cerrojo de T34, la operación <c>PreferenceChange</c> por
/// <see cref="RegistroDeCartera"/> —sin filas de libro, con el motivo y la constancia— y la versión nueva de <c>LND_PrepaymentPreferences</c> con
/// <c>Origin = DebtorRequest</c>. (nuevo)
/// </summary>
public sealed class ChangePrepaymentPreferenceCommandHandler(
    IApplicationDbContext db,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    PreferenciaDePrepagoVigente preferencias,
    ICurrentUserPermissions permisos,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ChangePrepaymentPreferenceCommand, Result<PrepaymentPreferenceDto>>
{
    public const string Permiso = "Lending.PrepaymentPreferences.Manage";

    public const string PrefijoDeCambio = "PRF-";

    public async Task<Result<PrepaymentPreferenceDto>> Handle(ChangePrepaymentPreferenceCommand request, CancellationToken ct)
    {
        if (!permisos.EsMaestroGlobal && !(await permisos.ListAsync(ct)).Contains(Permiso, StringComparer.OrdinalIgnoreCase))
            return Falla(ErroresDePagos.NoEncontrado());
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        if (leido.Value.Status != LoanStatus.Active) return Falla(ErroresDePagos.CreditoNoActivo(leido.Value.Number, leido.Value.Status.ToString()));
        var actor = await actorActual.ObtenerAsync(ct);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var credito = await db.Loans.FirstAsync(l => l.Id == leido.Value.Id, ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync([credito.Id], ModoDeEspera.Esperar, ct);
            if (credito.Status != LoanStatus.Active) return Falla(ErroresDePagos.CreditoNoActivo(credito.Number, credito.Status.ToString()));

            var control = await db.LoanControls.FirstAsync(c => c.LoanId == credito.Id, ct);
            var anterior = await preferencias.DelCreditoAsync(credito.Id, reloj.UtcNow, ct);
            var historia = await preferencias.HistoriaDelCreditoAsync(credito.Id, ct);
            var hoy = reloj.HoyLocal;
            var payload = JsonSerializer.Serialize(new
            {
                choice = request.Choice?.ToString(), previous = anterior?.Choice?.ToString(), evidence = request.EvidenceAttachmentPublicId,
            });
            var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.PreferenceChange, PrefijoDeCambio, ct);
            var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new LoanOperation
            {
                LoanId = credito.Id,
                Kind = LoanOperationKind.PreferenceChange,
                Number = numero,
                Sequence = sello.Sequence,
                ValueDate = hoy,
                AccountingDate = hoy,
                RegisteredAt = sello.RegisteredAt,
                Amount = 0m,
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                OriginUserCentralId = actor.CentralUserId,
                PrepaymentChoice = request.Choice,
                Reason = request.Reason.Trim(),
                Payload = payload,
                InputsSha256 = TramoDeDesembolso.Sha256(payload),
                RulesVersion = Domain.Lending.MotorDeCartera.VersionDeReglas,
                PrevalidationOutcome = PrevalidationOutcome.NotApplicable.ToString(),
            });
            await db.SaveChangesAsync(ct);

            var version = PrepaymentPreference.Registrar(credito.DebtorPersonId, credito.Id, null, request.Choice,
                (historia.Count == 0 ? 0 : historia.Max(p => p.VersionNumber)) + 1, operacion.RegisteredAt, PreferenceOrigin.DebtorRequest, actor.UserId,
                operationId: operacion.Id, evidencia: request.EvidenceAttachmentPublicId);
            db.PrepaymentPreferences.Add(version);
            await db.SaveChangesAsync(ct);
            return Result.Success(GetPrepaymentPreferenceQueryHandler.Dto([.. historia, version], operacion.RegisteredAt));
        }, ct);
    }

    private static Result<PrepaymentPreferenceDto> Falla(Error e) => Result.Failure<PrepaymentPreferenceDto>(e);
}
