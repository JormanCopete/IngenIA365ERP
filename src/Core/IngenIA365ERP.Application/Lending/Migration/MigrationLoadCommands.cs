using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>Una carga de migración (api.md §19.1). (nuevo)</summary>
public sealed record MigrationLoadDto(
    Guid PublicId,
    int Number,
    DateOnly CutoffDate,
    MigrationLoadStatus Status,
    string? Description,
    IReadOnlyList<MigrationLoadPartDto> Parts,
    DateTime? AppliedAt,
    string? AppliedBy,
    DateOnly? ActivationDeadline,
    bool IsBalanced,
    bool SalesReconciled,
    int LoanCount,
    MigrationDifferencesDto Differences,
    DateTime? ActivatedAt);

/// <summary>Una parte de la carga con sus filas y su huella (api.md §19.1). (nuevo)</summary>
public sealed record MigrationLoadPartDto(string Part, int Rows, string? Sha256, DateTime? ImportedAt);

/// <summary>Las diferencias de cuadre y conciliación de la carga: pendientes y aceptadas. (nuevo)</summary>
public sealed record MigrationDifferencesDto(int Pending, int Accepted);

/// <summary>
/// Crea una carga de migración en borrador con su corte (feature 013, T0232, C107; api.md §19.1, <c>POST /api/lending/migration/loads</c>;
/// data-model §29.1). La numera <see cref="NumeradorDeCartera"/>; un borrador anterior queda <c>Replaced</c> (hay a lo sumo uno). Con
/// Cartera activada no se crean cargas (<c>Lending.Activation.AlreadyActivated</c>). US8 la completa con las partes. (nuevo)
/// </summary>
public sealed record CreateMigrationLoadCommand(DateOnly CutoffDate, string? Notes) : IRequest<Result<MigrationLoadDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateMigrationLoadCommandValidator : AbstractValidator<CreateMigrationLoadCommand>
{
    public CreateMigrationLoadCommandValidator()
    {
        RuleFor(x => x.CutoffDate).NotEqual(default(DateOnly)).WithMessage("Indicá la fecha de corte.");
        RuleFor(x => x.Notes).MaximumLength(300).WithMessage("La descripción admite hasta 300 caracteres.");
    }
}

public sealed class CreateMigrationLoadCommandHandler(
    IApplicationDbContext db,
    NumeradorDeCartera numerador,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CreateMigrationLoadCommand, Result<MigrationLoadDto>>
{
    public async Task<Result<MigrationLoadDto>> Handle(CreateMigrationLoadCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<MigrationLoadDto>(ErroresDeActivacion.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var setup = await db.LendingSetups.AsNoTracking().FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct);
            if (setup?.ActivationDate is { } activada) return Result.Failure<MigrationLoadDto>(ErroresDeActivacion.YaActivada(activada));

            var ahora = reloj.UtcNow;
            // Hay a lo sumo un borrador (índice único filtrado): el anterior queda reemplazado y se guarda antes del nuevo.
            var anterior = await db.MigrationLoads.FirstOrDefaultAsync(l => l.Status == MigrationLoadStatus.Draft && !l.IsDeleted, ct);
            if (anterior is not null)
            {
                anterior.Status = MigrationLoadStatus.Replaced;
                anterior.ReplacedAt = ahora;
                await db.SaveChangesAsync(ct);
            }

            var carga = new MigrationLoad
            {
                CutoffDate = request.CutoffDate,
                Status = MigrationLoadStatus.Draft,
                Description = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedByUserId = usuario,
            };
            await numerador.NumerarCargaAsync(carga, ct);
            db.MigrationLoads.Add(carga);
            await db.SaveChangesAsync(ct);

            if (anterior is not null)
            {
                anterior.ReplacedByLoadId = carga.Id;
                await db.SaveChangesAsync(ct);
            }

            return Result.Success(await ConsultasDeCargas.DtoAsync(db, carga, ct));
        }, ct);
    }
}

/// <summary>Las cargas de migración, de la más nueva a la más vieja (api.md §19.1). (nuevo)</summary>
public sealed record ListMigrationLoadsQuery : IRequest<Result<IReadOnlyList<MigrationLoadDto>>>;

public sealed class ListMigrationLoadsQueryValidator : AbstractValidator<ListMigrationLoadsQuery>;

public sealed class ListMigrationLoadsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListMigrationLoadsQuery, Result<IReadOnlyList<MigrationLoadDto>>>
{
    public async Task<Result<IReadOnlyList<MigrationLoadDto>>> Handle(ListMigrationLoadsQuery request, CancellationToken ct)
    {
        var cargas = await db.MigrationLoads.AsNoTracking().Where(l => !l.IsDeleted).OrderByDescending(l => l.Number).ToListAsync(ct);
        var lista = new List<MigrationLoadDto>(cargas.Count);
        foreach (var carga in cargas) lista.Add(await ConsultasDeCargas.DtoAsync(db, carga, ct));
        return Result.Success<IReadOnlyList<MigrationLoadDto>>(lista);
    }
}

/// <summary>Una carga de migración con el resumen por parte (api.md §19.1). (nuevo)</summary>
public sealed record GetMigrationLoadQuery(Guid MigrationLoadPublicId) : IRequest<Result<MigrationLoadDto>>;

public sealed class GetMigrationLoadQueryValidator : AbstractValidator<GetMigrationLoadQuery>
{
    public GetMigrationLoadQueryValidator() => RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
}

public sealed class GetMigrationLoadQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMigrationLoadQuery, Result<MigrationLoadDto>>
{
    public async Task<Result<MigrationLoadDto>> Handle(GetMigrationLoadQuery request, CancellationToken ct)
    {
        var carga = await db.MigrationLoads.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == request.MigrationLoadPublicId && !l.IsDeleted, ct);
        return carga is null
            ? Result.Failure<MigrationLoadDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId))
            : Result.Success(await ConsultasDeCargas.DtoAsync(db, carga, ct));
    }
}

/// <summary>Lo que comparten las consultas y los comandos de cargas. (nuevo)</summary>
internal static class ConsultasDeCargas
{
    public static async Task<MigrationLoadDto> DtoAsync(IApplicationDbContext db, MigrationLoad carga, CancellationToken ct)
    {
        var partes = await db.MigrationLoadParts.AsNoTracking()
            .Where(p => p.MigrationLoadId == carga.Id && !p.IsDeleted)
            .OrderBy(p => p.PartKey).ThenBy(p => p.BatchNumber)
            .Select(p => new MigrationLoadPartDto(p.PartKey, p.RowCount, p.FileSha256, p.ImportedAt))
            .ToListAsync(ct);
        var diferencias = await db.MigrationLoadDifferences.AsNoTracking()
            .Where(d => d.MigrationLoadId == carga.Id && !d.IsDeleted)
            .Select(d => d.AcceptedAt)
            .ToListAsync(ct);
        var aplicadaPor = carga.AppliedByUserId is { } id
            ? await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Username).FirstOrDefaultAsync(ct)
            : null;
        return new MigrationLoadDto(carga.PublicId, carga.Number, carga.CutoffDate, carga.Status, carga.Description, partes, carga.AppliedAt,
            aplicadaPor, carga.ActivationDeadline, carga.IsBalanced, carga.SalesReconciled, carga.LoanCount,
            new MigrationDifferencesDto(diferencias.Count(a => a is null), diferencias.Count(a => a is not null)), carga.ActivatedAt);
    }
}
