using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans.Commands;

/// <summary>
/// <c>POST /api/lending/loans/{id}/validation-failure/confirm</c> (feature 013, US6, T0595; api.md §8.2; FR-038, FR-052, T63, B8;
/// <c>Lending.ValidationFailures.Confirm</c> con su límite, con clave y motivo): confirma por excepción la obligación de una venta con validación
/// fallida. (nuevo)
/// </summary>
public sealed record ConfirmValidationFailureCommand(Guid LoanPublicId, string Reason) : IRequest<Result<LoanDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfirmValidationFailureCommandValidator : ValidadorConMotivo<ConfirmValidationFailureCommand>
{
    public ConfirmValidationFailureCommandValidator() => RuleFor(x => x.LoanPublicId).NotEmpty();
}

/// <summary>
/// En una <see cref="TransaccionExplicita"/>, bajo el cerrojo del deudor:
/// <list type="number">
/// <item>el crédito en el alcance, con <c>SaleObligationStatus = ValidationFailed</c> sin confirmar y su recibo en <c>ValidationFailed</c>
/// (<c>Lending.Loan.NotValidationFailed</c>);</item>
/// <item>lo no exceptuable (FR-038: listas, usura, autorización, capacidad; <c>Lending.Attribution.NotExceptionable</c> con
/// <c>data.reasons[]</c>);</item>
/// <item>la atribución operativa: por encima del límite del usuario para <c>Lending.ValidationFailures.Confirm</c> no confirma —pasa a decisión de un
/// nivel que la alcance— y responde <c>Lending.Attribution.LevelRequired</c> (ver notas del bloque 10A);</item>
/// <item>la segregación por <see cref="RegistroDeIntervenciones"/> (acción <c>Decidir</c> sobre el crédito; <c>Lending.Segregation.Conflict</c>);</item>
/// <item>la decisión individual <c>LND_CreditDecisions</c> con el sujeto <c>ValidationFailureException</c> (aprueba por excepción, con el motivo); el
/// recibo pasa a <c>ConfirmedByException</c> con la decisión y el motivo, el crédito anota la confirmación y se levanta el bloqueo
/// <c>ValidationFailed</c>. La <b>entrega</b> de la bandeja queda en <c>ValidationFailed</c> como hecho histórico y no se emite mensaje (B8).</item>
/// </list>
/// </summary>
public sealed class ConfirmValidationFailureCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    RegistroDeIntervenciones intervenciones,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ConfirmValidationFailureCommand, Result<LoanDto>>
{
    public const string Permiso = "Lending.ValidationFailures.Confirm";

    public async Task<Result<LoanDto>> Handle(ConfirmValidationFailureCommand request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<LoanDto>(leido.Error);

        var r = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var credito = await db.Loans.FirstAsync(l => l.PublicId == request.LoanPublicId, ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);

            var recibo = await db.LendingInboundMessages.Where(x => x.LoanId == credito.Id && x.Status == InboundMessageStatus.ValidationFailed)
                .OrderByDescending(x => x.LastProcessedAt).FirstOrDefaultAsync(ct);
            if (credito.SaleObligationStatus != SaleObligationStatus.ValidationFailed || credito.ConfirmedByExceptionAt is not null || recibo is null)
                return Result.Failure(ErroresDeVentasACredito.SinValidacionFallida(credito.Number));

            // Lo no exceptuable se mira en todas las verificaciones que fallaron, no sólo en la primera (FR-038).
            var evaluacion = recibo.LastEvaluationId is { } ultima
                ? await db.LendingInboundMessageEvaluations.AsNoTracking().FirstOrDefaultAsync(e => e.Id == ultima, ct)
                : null;
            var noExceptuables = CodigosFallidos(evaluacion?.ChecksJson).Append(credito.ValidationFailureCode)
                .Select(EvaluacionDeVentaACredito.NoExceptuable).OfType<MotivoNoExceptuable>().ToList();
            if (noExceptuables.Count > 0) return Result.Failure(ErroresDeDecisionesYCupos.NoExceptuable(NoExceptuables.Evaluar(noExceptuables)));

            var hoy = reloj.HoyLocal;
            var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(nameof(Domain.Entities.Lending.Loans.Loan), credito.PublicId),
                AccionesDeSegregacion.Decidir, hoy, ct);
            if (verificada.IsFailure) return Result.Failure(verificada.Error);
            var v = verificada.Value;

            if (await LimitesDePermisoPorUsuario.DeAsync(db, v.UsuarioId, Permiso, hoy, ct) is { } maximo && maximo < credito.ApprovedAmount)
                return Result.Failure(new ErrorConDatos(ErroresDeDecisionesYCupos.CodigoNivelRequerido,
                    $"El crédito {credito.Number} ({ErroresDeOriginacion.Pesos(credito.ApprovedAmount)}) supera su atribución para confirmar por excepción "
                    + $"({ErroresDeOriginacion.Pesos(maximo)}): pásela a decisión de quien tenga el nivel que la alcance.",
                    new { amount = credito.ApprovedAmount, limit = maximo, permission = Permiso }));

            var motivo = request.Reason.Trim();
            var ahora = reloj.UtcNow;
            var decision = CreditDecision.Individual(new LoDecidido(LendingDecisionSubjects.ValidationFailureException, credito.PublicId, credito.PublicId,
                CreditDecisionOutcome.Approve, credito.ApprovedAmount)
            {
                Reason = motivo,
                IsPolicyException = true,
            }, v.UsuarioId, v.PersonaId, ahora);
            db.CreditDecisions.Add(decision);
            intervenciones.Registrar(v, LendingDecisionSubjects.ValidationFailureException, credito.PublicId);
            await db.SaveChangesAsync(ct);

            recibo.Status = InboundMessageStatus.ConfirmedByException;
            recibo.ExceptionDecisionId = decision.Id;
            recibo.ExceptionReason = motivo.Length <= 1000 ? motivo : motivo[..1000];
            recibo.ConfirmedByExceptionAt = ahora;
            recibo.ConfirmedByExceptionByUserId = v.UsuarioId;
            credito.ConfirmedByExceptionAt = ahora;
            credito.ConfirmedByExceptionDecisionId = decision.Id;
            var clave = NacimientoDeObligacionDeVenta.ClaveDelBloqueo(credito.PublicId);
            foreach (var bloqueo in await db.DebtorBlocks.Where(b => b.PersonId == credito.DebtorPersonId && b.Reason == DebtorBlockReason.ValidationFailed
                                                                     && b.SourceKey == clave && b.LiftedAt == null && !b.IsDeleted).ToListAsync(ct))
                bloqueo.Levantar(hoy, ahora, v.UsuarioId, $"Confirmada por excepción: {motivo}");
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
        if (r.IsFailure) return Result.Failure<LoanDto>(r.Error);

        db.DescartarCambios();
        return await new GetLoanQueryHandler(db, alcance, auditoria).Handle(new GetLoanQuery(request.LoanPublicId), ct);
    }

    /// <summary>Los códigos de las verificaciones que fallaron en una evaluación (<c>ChecksJson</c>).</summary>
    private static IEnumerable<string?> CodigosFallidos(string? checksJson)
    {
        if (string.IsNullOrWhiteSpace(checksJson)) return [];
        try
        {
            using var documento = System.Text.Json.JsonDocument.Parse(checksJson);
            return documento.RootElement.EnumerateArray()
                .Where(c => c.TryGetProperty("passed", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.False)
                .Select(c => c.TryGetProperty("code", out var k) ? k.GetString() : null)
                .ToList();
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}
