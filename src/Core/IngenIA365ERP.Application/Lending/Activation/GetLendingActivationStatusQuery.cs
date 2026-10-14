using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>
/// El estado de la activación (api.md §19.3, <c>ActivationReadinessDto</c>): si Cartera está activada y desde cuándo, la carga vigente
/// (la aplicada, o el borrador), la fecha que correspondería (corte + 1), el plazo, los requisitos con su estado y quién los corrige, los avisos que
/// no bloquean y la marca «modelo obligatorio no disponible» de una cooperativa obligada fuera de producción (US8, T0747). (nuevo)
/// </summary>
public sealed record ActivationReadinessDto(
    string Status,
    DateOnly? ActivationDate,
    Guid? ActivationPublicId,
    DateTime? ActivatedAt,
    ActivationLoadDto? CurrentLoad,
    DateOnly? ExpectedActivationDate,
    DateOnly? ActivationDeadline,
    bool IsProductionEnvironment,
    IReadOnlyList<RequisitoDeActivacionDto> Requirements,
    IReadOnlyList<AvisoDeActivacionDto>? Warnings = null,
    bool ObligatoryModelUnavailable = false);

/// <summary>La carga que se activaría. (nuevo)</summary>
public sealed record ActivationLoadDto(Guid PublicId, int Number, DateOnly CutoffDate, MigrationLoadStatus Status);

/// <summary>
/// El estado de la activación de Cartera (feature 013, T0233; api.md §19.3, <c>GET /api/lending/activation</c>). Sólo lee. (nuevo)
/// </summary>
public sealed record GetLendingActivationStatusQuery : IRequest<Result<ActivationReadinessDto>>;

public sealed class GetLendingActivationStatusQueryValidator : AbstractValidator<GetLendingActivationStatusQuery>;

public sealed class GetLendingActivationStatusQueryHandler(
    IApplicationDbContext db,
    RequisitosDeActivacion requisitos,
    IDateTimeService reloj) : IRequestHandler<GetLendingActivationStatusQuery, Result<ActivationReadinessDto>>
{
    public const string Activada = "Activated";
    public const string SinActivar = "NotActivated";

    public async Task<Result<ActivationReadinessDto>> Handle(GetLendingActivationStatusQuery request, CancellationToken ct)
    {
        var setup = await db.LendingSetups.AsNoTracking().FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct);
        var hoy = reloj.HoyLocal;

        if (setup?.ActivationDate is { } fecha)
        {
            var activacion = await db.LendingActivations.AsNoTracking()
                .Where(a => a.Id == setup.ActivationId)
                .Select(a => new { a.PublicId, a.MigrationLoadId, a.RequirementsJson })
                .FirstOrDefaultAsync(ct);
            var activada = activacion is null ? null : await db.MigrationLoads.AsNoTracking()
                .Where(l => l.Id == activacion.MigrationLoadId)
                .Select(l => new ActivationLoadDto(l.PublicId, l.Number, l.CutoffDate, l.Status))
                .FirstOrDefaultAsync(ct);
            return Result.Success(new ActivationReadinessDto(Activada, fecha, activacion?.PublicId, setup.ActivatedAt, activada, fecha, null,
                requisitos.EsProduccion, []));
        }

        var carga = await db.MigrationLoads.AsNoTracking().Where(l => l.Status == MigrationLoadStatus.Applied && !l.IsDeleted).FirstOrDefaultAsync(ct)
            ?? await db.MigrationLoads.AsNoTracking().Where(l => l.Status == MigrationLoadStatus.Draft && !l.IsDeleted).FirstOrDefaultAsync(ct);
        var esperada = carga?.CutoffDate.AddDays(1);
        var evaluacion = await requisitos.EvaluarConAvisosAsync(carga, esperada ?? hoy, hoy, ct);
        return Result.Success(new ActivationReadinessDto(SinActivar, null, null, null,
            carga is null ? null : new ActivationLoadDto(carga.PublicId, carga.Number, carga.CutoffDate, carga.Status),
            esperada, carga?.ActivationDeadline, requisitos.EsProduccion, evaluacion.Requisitos, evaluacion.Avisos, evaluacion.ObligatoryModelUnavailable));
    }
}
