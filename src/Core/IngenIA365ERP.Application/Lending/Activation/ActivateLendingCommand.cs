using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>Lo que responde la activación (api.md §19.3). (nuevo)</summary>
public sealed record LendingActivationDto(
    Guid ActivationPublicId,
    DateOnly ActivationDate,
    int Loans,
    int OpeningTransactions,
    Guid? BaseClosePublicId,
    int CreditLimits,
    int RevalidatedLimits,
    int NonDeductionsMarked,
    int ProvisionalCollectionsStored,
    IReadOnlyList<MensajeEmitidoDto> Messages);

/// <summary>Un mensaje que nació con la activación. (nuevo)</summary>
public sealed record MensajeEmitidoDto(string Type, Guid MessagePublicId);

/// <summary>
/// Activa Cartera en la cooperativa (feature 013, T0233, C60, C61, C107; FR-089, FR-142, T84; api.md §19.3, <c>POST /api/lending/activation</c>;
/// data-model §30.1, §32.3). Contrato completo de la ruta; <b>camino mínimo</b> para una carga sin créditos:
/// <list type="number">
/// <item>Cartera no activada (<c>Lending.Activation.AlreadyActivated</c>) y la carga existe;</item>
/// <item>fecha de activación = corte + 1 (<c>Lending.Activation.CutoffInvalid</c>, <c>data.expected</c>);</item>
/// <item>acta obligatoria (C61): <see cref="SessionItemPublicId"/> es un punto <c>Decision</c> aprobado de una sesión cerrada cuyo sujeto
/// es <c>ActivationDifferences</c> de esa carga (<c>Lending.Session.ItemMismatch</c>);</item>
/// <item>requisitos de <see cref="RequisitosDeActivacion"/> (<c>Lending.Activation.RequirementsMissing</c>, <c>data.missing[]</c>);</item>
/// <item>en una <see cref="TransaccionExplicita"/>, con <c>LND_Setup</c> exclusivo: <c>LND_Activations</c>, <c>LND_Setup.ActivationDate</c>,
/// <c>ActivationId</c> y <c>ActivatedAt</c> (una sola vez), la carga <c>Activated</c>, el destino <c>Lending</c> habilitado en el registro
/// por <see cref="EscritorDelRegistroDeIntegracion"/> en la misma transacción (C60) y <c>CarteraActivada</c> por <see cref="EmisionDeCartera"/>.</item>
/// </list>
/// US17 cambia la validación del punto por <c>PuntoDeActaCerrado</c> (T0311); US9 cablea el cierre base (T0708); US8 completa los
/// requisitos, el nacimiento de créditos por tandas de 1.000 con <c>ChangeTracker.Clear()</c> y los cobros provisionales (C60, C62). (nuevo)
/// </summary>
public sealed record ActivateLendingCommand(
    Guid MigrationLoadPublicId,
    DateOnly ActivationDate,
    Guid? ProvisionalCollectionsAttachmentPublicId,
    Guid SessionItemPublicId,
    bool AcknowledgeWarnings,
    string Reason) : IRequest<Result<LendingActivationDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ActivateLendingCommandValidator : ValidadorConMotivo<ActivateLendingCommand>
{
    public ActivateLendingCommandValidator()
    {
        RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
        RuleFor(x => x.ActivationDate).NotEqual(default(DateOnly)).WithMessage("Indicá la fecha de activación.");
    }
}

public sealed class ActivateLendingCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    RequisitosDeActivacion requisitos,
    EscritorDelRegistroDeIntegracion escritorDelRegistro,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ActivateLendingCommand, Result<LendingActivationDto>>
{
    /// <summary>El código del destino de Cartera que siembra <c>PlataformaParaCartera</c> inhabilitado (C35).</summary>
    public const string CodigoDelDestinoDeCartera = "CAR";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<LendingActivationDto>> Handle(ActivateLendingCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<LendingActivationDto>(ErroresDeActivacion.SinUsuario());

        // Lo que se puede rechazar sin cerrojo (las lecturas se repiten adentro).
        var previa = await ValidarAsync(request, ct);
        if (previa.IsFailure) return Result.Failure<LendingActivationDto>(previa.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: true, ct);
            var setup = await db.LendingSetups.FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct)
                ?? throw new InvalidOperationException("Falta la fila única de LND_Setup: la migración CarteraNucleo no se aplicó.");
            var validado = await ValidarAsync(request, ct);
            if (validado.IsFailure) return Result.Failure<LendingActivationDto>(validado.Error);
            var (carga, punto, lista) = validado.Value;

            var ahora = reloj.UtcNow;
            var activacionPublicId = Guid.NewGuid();
            var mensaje = await emision.ActivacionAsync(activacionPublicId, request.ActivationDate, carga, cierreBase: null, capitalTotal: 0m, ct);
            if (mensaje.IsFailure) return Result.Failure<LendingActivationDto>(mensaje.Error);

            var activacion = new LendingActivation
            {
                PublicId = activacionPublicId,
                MigrationLoadId = carga.Id,
                CutoffDate = carga.CutoffDate,
                ActivationDate = request.ActivationDate,
                RegisteredByUserId = usuario,
                RegisteredAt = ahora,
                SessionItemId = punto,
                CheckVersion = carga.CurrentCheckVersion,
                BalanceCheckSha256 = carga.BalanceCheckSha256 ?? ApplyMigrationLoadCommandHandler.HuellaVacia,
                SalesReconciliationSha256 = carga.SalesReconciliationSha256 ?? ApplyMigrationLoadCommandHandler.HuellaVacia,
                IsProductionEnvironment = requisitos.EsProduccion,
                RequirementsJson = JsonSerializer.Serialize(lista.Select(r => new { r.Code, r.Met, r.Detail }), Json),
                ObligatoryModelUnavailable = false,
                CountsJson = JsonSerializer.Serialize(new
                {
                    loans = 0, openingTransactions = 0, writtenOff = 0, creditLimits = 0, blocks = 0, authorizations = 0,
                    priorNotices = 0, claims = 0, relatedParties = 0, collaterals = 0,
                }, Json),
                NonDeductionsMarked = 0,
                CreditLimitsToRevalidate = 0,
                ProvisionalCollectionsCount = 0,
                CommercialAdjustmentsJson = "{}",
                PayrollWarningAcknowledged = request.AcknowledgeWarnings,
                ActivatedMessagePublicId = mensaje.Value.PublicId,
            };
            db.LendingActivations.Add(activacion);

            carga.Status = MigrationLoadStatus.Activated;
            carga.ActivatedAt = ahora;

            var destino = await escritorDelRegistro.CambiarHabilitacionAsync(CodigoDelDestinoDeCartera, IntegrationParticipantRole.MessageDestination,
                IntegrationDestinations.Lending, habilitado: true, request.ActivationDate, $"Activación de Cartera: {request.Reason.Trim()}", ct);
            if (destino.IsFailure) return Result.Failure<LendingActivationDto>(destino.Error);

            await db.SaveChangesAsync(ct);

            setup.ActivationDate = request.ActivationDate;
            setup.ActivationId = activacion.Id;
            setup.ActivatedAt = ahora;
            await db.SaveChangesAsync(ct);

            return Result.Success(new LendingActivationDto(activacion.PublicId, activacion.ActivationDate, 0, 0, null, 0, 0, 0, 0,
                [new MensajeEmitidoDto(mensaje.Value.Type, mensaje.Value.PublicId)]));
        }, ct);
    }

    /// <summary>Activada, carga, fecha, acta y requisitos, en ese orden (T0147). Devuelve la carga con seguimiento y el Id del punto.</summary>
    private async Task<Result<(MigrationLoad Carga, int Punto, IReadOnlyList<RequisitoDeActivacionDto> Requisitos)>> ValidarAsync(
        ActivateLendingCommand request, CancellationToken ct)
    {
        var setup = await db.LendingSetups.AsNoTracking().FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct);
        if (setup?.ActivationDate is { } activada) return Falla(ErroresDeActivacion.YaActivada(activada));

        var carga = await db.MigrationLoads.FirstOrDefaultAsync(l => l.PublicId == request.MigrationLoadPublicId && !l.IsDeleted, ct);
        if (carga is null) return Falla(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));

        if (request.ActivationDate != carga.CutoffDate.AddDays(1)) return Falla(ErroresDeActivacion.CorteInvalido(carga.CutoffDate));

        var punto = await db.SessionItems.AsNoTracking()
            .Where(i => i.PublicId == request.SessionItemPublicId && !i.IsDeleted)
            .Join(db.LendingSessions.AsNoTracking(), i => i.SessionId, s => s.Id, (i, s) => new
            {
                i.Id, i.Kind, i.Outcome, i.SubjectType, i.SubjectPublicId, SessionStatus = s.Status,
            })
            .FirstOrDefaultAsync(ct);
        var corresponde = punto is not null
            && punto.SessionStatus == SessionStatus.Closed
            && punto.Kind == SessionItemKind.Decision
            && punto.Outcome is CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions
            && punto.SubjectType == LendingDecisionSubjects.ActivationDifferences
            && punto.SubjectPublicId == carga.PublicId;
        if (!corresponde)
            return Falla(ErroresDeActivacion.PuntoNoCorresponde(LendingDecisionSubjects.ActivationDifferences, punto?.SubjectType, punto?.Outcome?.ToString()));

        var lista = await requisitos.EvaluarAsync(carga, request.ActivationDate, reloj.HoyLocal, ct);
        var faltantes = lista.Where(r => !r.Met).ToList();
        if (faltantes.Count > 0) return Falla(ErroresDeActivacion.FaltanRequisitos(faltantes));

        return Result.Success<(MigrationLoad, int, IReadOnlyList<RequisitoDeActivacionDto>)>((carga, punto!.Id, lista));
    }

    private static Result<(MigrationLoad, int, IReadOnlyList<RequisitoDeActivacionDto>)> Falla(Error error) =>
        Result.Failure<(MigrationLoad, int, IReadOnlyList<RequisitoDeActivacionDto>)>(error);
}
