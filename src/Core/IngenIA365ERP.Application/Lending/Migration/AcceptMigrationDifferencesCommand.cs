using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>
/// Acepta diferencias del cuadre y de la conciliación de ventas de una carga aplicada (feature 013, US8, T0744; D17, T64, T66; api.md §19.2,
/// <c>POST /api/lending/migration/loads/{id}/differences/accept</c>, <c>Lending.Migration.AcceptDifferences</c>). (nuevo)
/// </summary>
public sealed record AcceptMigrationDifferencesCommand(
    Guid MigrationLoadPublicId,
    IReadOnlyList<Guid> DifferencePublicIds,
    Guid SessionItemPublicId,
    string Reason) : IRequest<Result<IReadOnlyList<MigrationDifferenceDto>>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class AcceptMigrationDifferencesCommandValidator : ValidadorConMotivo<AcceptMigrationDifferencesCommand>
{
    public AcceptMigrationDifferencesCommandValidator()
    {
        RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
        RuleFor(x => x.SessionItemPublicId).NotEmpty().WithMessage("Indique el punto de la sesión que aprueba las diferencias.");
        RuleFor(x => x.DifferencePublicIds).NotEmpty().WithMessage("Elija al menos una diferencia para aceptar.");
    }
}

/// <summary>
/// Cada diferencia guarda su propia aceptación (escritura única: <c>AcceptedAt</c>, <c>AcceptedByUserId</c>, <c>AcceptanceReason</c>,
/// <c>SessionItemId</c>), venta por venta en las de ventas. Exige:
/// <list type="bullet">
/// <item>un punto de sesión <b>cerrado</b> de decisión, aprobado, con sujeto <c>ActivationDifferences</c> <b>de esta carga</b>
/// (<see cref="PuntoDeActaCerrado"/>; si no, <c>Lending.Session.ItemMismatch</c>);</item>
/// <item>la segregación: con el par <c>CargarMigracion × AceptarDiferencias</c> activo en <c>Segregacion.Incompatibilidades</c>, quien cargó la
/// migración no acepta (<c>Lending.Segregation.Conflict</c>, por <see cref="RegistroDeIntervenciones"/>);</item>
/// <item>diferencias <b>vigentes</b> de la versión actual del cuadre y sin aceptar (si no, <c>Lending.MigrationLoad.DifferenceNotFound</c>): la
/// aceptación es de esa carga y de esa versión; si la activación recalcula y cambia, hay que volver a aceptar.</item>
/// </list>
/// Actualiza <c>IsBalanced</c> y <c>SalesReconciled</c> de la carga.
/// </summary>
public sealed class AcceptMigrationDifferencesCommandHandler(
    IApplicationDbContext db,
    PuntoDeActaCerrado puntoDeActa,
    RegistroDeIntervenciones intervenciones,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<AcceptMigrationDifferencesCommand, Result<IReadOnlyList<MigrationDifferenceDto>>>
{
    public async Task<Result<IReadOnlyList<MigrationDifferenceDto>>> Handle(AcceptMigrationDifferencesCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<IReadOnlyList<MigrationDifferenceDto>>(ErroresDeActivacion.SinUsuario());
        var carga = await db.MigrationLoads.FirstOrDefaultAsync(l => l.PublicId == request.MigrationLoadPublicId && !l.IsDeleted, ct);
        if (carga is null) return Result.Failure<IReadOnlyList<MigrationDifferenceDto>>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));

        var punto = await puntoDeActa.ValidarAsync(request.SessionItemPublicId, LendingDecisionSubjects.ActivationDifferences, carga.PublicId, ct);
        if (punto.IsFailure) return Result.Failure<IReadOnlyList<MigrationDifferenceDto>>(punto.Error);

        var expediente = new ExpedienteDeCartera(AsuntoDeDiferenciasDeActivacion.ClaseDeExpediente, carga.PublicId);
        var verificada = await intervenciones.VerificarAsync(expediente, AccionesDeSegregacion.AceptarDiferencias, reloj.HoyLocal, ct);
        if (verificada.IsFailure) return Result.Failure<IReadOnlyList<MigrationDifferenceDto>>(verificada.Error);

        var pedidas = request.DifferencePublicIds.Distinct().ToList();
        var vigentes = carga.Status == MigrationLoadStatus.Applied
            ? await ConciliacionDeVentasDeMigracion.VigentesAsync(db, carga, carga.CurrentCheckVersion, ct)
            : [];
        var elegidas = vigentes.Where(d => pedidas.Contains(d.PublicId)).ToList();
        if (elegidas.Count != pedidas.Count || elegidas.Any(d => d.Aceptada))
            return Result.Failure<IReadOnlyList<MigrationDifferenceDto>>(ErroresDeMigracion.DiferenciaInexistente(pedidas));

        var ahora = reloj.UtcNow;
        foreach (var d in elegidas) d.Aceptar(usuario, request.Reason, punto.Value.ItemId, ahora);
        intervenciones.Registrar(verificada.Value, AsuntoDeDiferenciasDeActivacion.ClaseDeExpediente, carga.PublicId);
        ConciliacionDeVentasDeMigracion.Actualizar(carga, vigentes);
        await db.SaveChangesAsync(ct);

        return Result.Success<IReadOnlyList<MigrationDifferenceDto>>(elegidas.Select(d => new MigrationDifferenceDto(d.PublicId, d.Kind, d.DimensionKey,
            d.ConceptFamily, d.ModalityCode, d.SubsegmentCode, d.RiskCategory?.ToString(), d.AccountSetDisplay, d.InvoiceNumber, d.LendingAmount,
            d.AccountingAmount, d.Difference, d.AcceptedAt, d.AcceptanceReason)).ToList());
    }
}
