using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>Un total cargado por línea y concepto (las cifras de la carga que se cotejan con las de referencia, FR-118). (nuevo)</summary>
public sealed record TotalDeCargaDto(string LineCode, string Concept, decimal Amount, int Accounts);

/// <summary>Una parte de la carga con sus tandas (api.md §16 de la 014). (nuevo)</summary>
public sealed record DepositMigrationPartSummaryDto(string Part, string Name, bool Required, bool Available, string? AvailableWith, int Batches, int Rows);

/// <summary>Una carga de migración de aportes o de captaciones (api.md §16 de la 014). (nuevo)</summary>
public sealed record DepositMigrationLoadDto(
    Guid PublicId,
    int Number,
    LineNature Nature,
    DateOnly CutoffDate,
    MigrationLoadStatus Status,
    string? Description,
    IReadOnlyList<MigrationLoadPartDto> Parts,
    IReadOnlyList<DepositMigrationPartSummaryDto> PartsSummary,
    DateTime? AppliedAt,
    string? AppliedBy,
    DateOnly? ActivationDeadline,
    bool IsBalanced,
    int AccountCount,
    MigrationDifferencesDto Differences,
    DateTime? ActivatedAt,
    IReadOnlyList<TotalDeCargaDto> Totals,
    IReadOnlyList<ErrorEntrePartes> Warnings);

/// <summary>Los errores de la migración de aportes y captaciones que no tiene la de créditos (feature 014, US9; plantillas-y-archivos.md §4.9). (nuevo)</summary>
public static class ErroresDeMigracionDeCaptaciones
{
    public const string CodigoParteDeOtraNaturaleza = "Lending.Migration.PartNotForNature";
    public const string CodigoParteNoDisponible = "Lending.Migration.PartNotAvailable";
    public const string CodigoNumeroDuplicado = "Lending.Migration.AccountNumberDuplicate";
    public const string CodigoSinEquivalencia = "Lending.Migration.LineEquivalenceMissing";
    public const string CodigoMenorSinRepresentante = "Lending.Migration.MinorWithoutRepresentative";
    public const string CodigoCuentaNoEnLaCarga = "Lending.Migration.AccountNotInLoad";
    public const string CodigoNaturalezaInvalida = "Lending.Migration.NatureInvalid";

    public static Error ParteDeOtraNaturaleza(string parte, LineNature naturaleza) =>
        new ErrorConDatos(CodigoParteDeOtraNaturaleza,
            $"La parte {parte} no es de una carga de {Nombre(naturaleza)}: elija una parte de su naturaleza.",
            new { part = parte, nature = naturaleza.ToString() });

    public static Error ParteNoDisponible(ParteDeMigracionDeCaptaciones parte) =>
        new ErrorConDatos(CodigoParteNoDisponible,
            parte.LlegaCon is { } historia
                ? $"La parte {parte.Parte} ({parte.Nombre}) llega con {historia}: todavía no se carga."
                : $"La parte {parte.Parte} ({parte.Nombre}) llega con la entrega {parte.Entrega} de la Cartera, que todavía no está desplegada.",
            new { part = parte.Parte, availableFrom = parte.Entrega.ToString() });

    public static Error NaturalezaInvalida() =>
        new ErrorConDatos(CodigoNaturalezaInvalida, "Indique de qué naturaleza es la carga: Deposit (captaciones) o Contribution (aportes).", new { field = "nature" });

    public static string Nombre(LineNature naturaleza) => naturaleza == LineNature.Contribution ? "aportes" : "captaciones";
}

/// <summary>
/// Crea una carga de migración de aportes o de captaciones en borrador con su corte (feature 014, US9, T153; api.md §16, <c>POST
/// /api/lending/deposits/migration/loads</c>, <c>Lending.Deposits.Migration</c>; data-model §7). Un borrador anterior <b>de la misma naturaleza</b> queda
/// <c>Replaced</c> (a lo sumo uno por naturaleza); con la naturaleza activada no se crean cargas (<c>Lending.Activation.AlreadyActivated</c> con
/// <c>data.nature</c>), y sin su entrega desplegada tampoco (<c>Lending.Savings.DeliveryNotAvailable</c>). (nuevo)
/// </summary>
public sealed record CreateDepositMigrationLoadCommand(LineNature Nature, DateOnly CutoffDate, string? Notes)
    : IRequest<Result<DepositMigrationLoadDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateDepositMigrationLoadCommandValidator : AbstractValidator<CreateDepositMigrationLoadCommand>
{
    public CreateDepositMigrationLoadCommandValidator()
    {
        RuleFor(x => x.CutoffDate).NotEqual(default(DateOnly)).WithMessage("Indique la fecha de corte.");
        RuleFor(x => x.Notes).MaximumLength(300).WithMessage("La descripción admite hasta 300 caracteres.");
    }
}

public sealed class CreateDepositMigrationLoadCommandHandler(
    IApplicationDbContext db,
    NumeradorDeCartera numerador,
    RequisitosDeActivacionDeCaptaciones requisitos,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CreateDepositMigrationLoadCommand, Result<DepositMigrationLoadDto>>
{
    public async Task<Result<DepositMigrationLoadDto>> Handle(CreateDepositMigrationLoadCommand request, CancellationToken ct)
    {
        if (request.Nature is not (LineNature.Deposit or LineNature.Contribution))
            return Result.Failure<DepositMigrationLoadDto>(ErroresDeMigracionDeCaptaciones.NaturalezaInvalida());
        if (requisitos.EntregaNoDisponible(request.Nature) is { } entrega) return Result.Failure<DepositMigrationLoadDto>(entrega);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<DepositMigrationLoadDto>(ErroresDeActivacion.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
            if (fechas.De(request.Nature) is { } activada)
                return Result.Failure<DepositMigrationLoadDto>(ErroresDeActivacionDeCaptaciones.YaActivada(request.Nature, activada));

            var ahora = reloj.UtcNow;
            var anterior = await db.MigrationLoads.FirstOrDefaultAsync(l => l.Nature == request.Nature && l.Status == MigrationLoadStatus.Draft && !l.IsDeleted, ct);
            if (anterior is not null)
            {
                anterior.Reemplazar(null, ahora);
                await db.SaveChangesAsync(ct);
            }

            var carga = new MigrationLoad
            {
                Nature = request.Nature,
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
            return Result.Success(await ConsultasDeCargasDeCaptaciones.DtoAsync(db, carga, requisitos.Entrega, ct));
        }, ct);
    }
}

/// <summary>Las cargas de aportes y captaciones (de una naturaleza, o de las dos), de la más nueva a la más vieja (api.md §16). (nuevo)</summary>
public sealed record ListDepositMigrationLoadsQuery(LineNature? Nature = null) : IRequest<Result<IReadOnlyList<DepositMigrationLoadDto>>>;

public sealed class ListDepositMigrationLoadsQueryValidator : AbstractValidator<ListDepositMigrationLoadsQuery>;

public sealed class ListDepositMigrationLoadsQueryHandler(IApplicationDbContext db, RequisitosDeActivacionDeCaptaciones requisitos)
    : IRequestHandler<ListDepositMigrationLoadsQuery, Result<IReadOnlyList<DepositMigrationLoadDto>>>
{
    public async Task<Result<IReadOnlyList<DepositMigrationLoadDto>>> Handle(ListDepositMigrationLoadsQuery request, CancellationToken ct)
    {
        var cargas = await db.MigrationLoads.AsNoTracking()
            .Where(l => l.Nature != LineNature.Placement && !l.IsDeleted && (request.Nature == null || l.Nature == request.Nature))
            .OrderByDescending(l => l.Number).ToListAsync(ct);
        var lista = new List<DepositMigrationLoadDto>(cargas.Count);
        foreach (var carga in cargas) lista.Add(await ConsultasDeCargasDeCaptaciones.DtoAsync(db, carga, requisitos.Entrega, ct));
        return Result.Success<IReadOnlyList<DepositMigrationLoadDto>>(lista);
    }
}

/// <summary>Una carga de aportes o captaciones con el resumen por parte (api.md §16). Una de créditos responde <c>Lending.MigrationLoad.NotFound</c>. (nuevo)</summary>
public sealed record GetDepositMigrationLoadQuery(Guid MigrationLoadPublicId) : IRequest<Result<DepositMigrationLoadDto>>;

public sealed class GetDepositMigrationLoadQueryValidator : AbstractValidator<GetDepositMigrationLoadQuery>
{
    public GetDepositMigrationLoadQueryValidator() => RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
}

public sealed class GetDepositMigrationLoadQueryHandler(IApplicationDbContext db, RequisitosDeActivacionDeCaptaciones requisitos)
    : IRequestHandler<GetDepositMigrationLoadQuery, Result<DepositMigrationLoadDto>>
{
    public async Task<Result<DepositMigrationLoadDto>> Handle(GetDepositMigrationLoadQuery request, CancellationToken ct)
    {
        var carga = await ConsultasDeCargasDeCaptaciones.BuscarAsync(db, request.MigrationLoadPublicId, seguimiento: false, ct);
        return carga is null
            ? Result.Failure<DepositMigrationLoadDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId))
            : Result.Success(await ConsultasDeCargasDeCaptaciones.DtoAsync(db, carga, requisitos.Entrega, ct));
    }
}

/// <summary>Lo que comparten las consultas y los comandos de las cargas de aportes y captaciones. (nuevo)</summary>
public static class ConsultasDeCargasDeCaptaciones
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Una carga de aportes o captaciones por su <c>PublicId</c>; nula si no existe o es de créditos (la de créditos es de su propia ruta).</summary>
    public static Task<MigrationLoad?> BuscarAsync(IApplicationDbContext db, Guid publicId, bool seguimiento, CancellationToken ct)
    {
        var consulta = seguimiento ? db.MigrationLoads : db.MigrationLoads.AsNoTracking();
        return consulta.FirstOrDefaultAsync(l => l.PublicId == publicId && l.Nature != LineNature.Placement && !l.IsDeleted, ct);
    }

    /// <summary>Las tandas vivas de la carga, como consulta de sus Id.</summary>
    public static IQueryable<int> PartesVivas(IApplicationDbContext db, int carga, string parte) => db.MigrationLoadParts.AsNoTracking()
        .Where(p => p.MigrationLoadId == carga && p.PartKey == parte && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).Select(p => p.Id);

    public static async Task<DepositMigrationLoadDto> DtoAsync(IApplicationDbContext db, MigrationLoad carga, Domain.Lending.EntregaDeCartera entrega,
        CancellationToken ct)
    {
        var partes = await db.MigrationLoadParts.AsNoTracking()
            .Where(p => p.MigrationLoadId == carga.Id && !p.IsDeleted)
            .OrderBy(p => p.PartKey).ThenBy(p => p.BatchNumber)
            .Select(p => new MigrationLoadPartDto(p.PartKey, p.RowCount, p.FileSha256, p.ImportedAt, p.BatchNumber, p.FileName, p.Status))
            .ToListAsync(ct);
        var resumen = DefinicionesDeMigracionDeCaptaciones.Todas.Where(p => p.Admite(carga.Nature)).OrderBy(p => p.Orden)
            .Select(p =>
            {
                var vivas = partes.Where(t => t.Part == p.Parte && t.Status == MigrationPartStatus.Imported).ToList();
                return new DepositMigrationPartSummaryDto(p.Parte, p.Nombre, p.ObligatoriaEn(carga.Nature), p.Disponible(entrega), p.LlegaCon, vivas.Count,
                    vivas.Sum(t => t.Rows));
            }).ToList();
        var diferencias = carga.CurrentCheckVersion == 0
            ? []
            : await db.MigrationLoadDifferences.AsNoTracking()
                .Where(d => d.MigrationLoadId == carga.Id && d.CheckVersion == carga.CurrentCheckVersion && !d.IsDeleted)
                .Select(d => d.AcceptedAt).ToListAsync(ct);
        var aplicadaPor = carga.AppliedByUserId is { } id
            ? await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Username).FirstOrDefaultAsync(ct)
            : null;
        return new DepositMigrationLoadDto(carga.PublicId, carga.Number, carga.Nature, carga.CutoffDate, carga.Status, carga.Description, partes, resumen,
            carga.AppliedAt, aplicadaPor, carga.ActivationDeadline, carga.IsBalanced, carga.AccountCount,
            new MigrationDifferencesDto(diferencias.Count(a => a is null), diferencias.Count(a => a is not null)), carga.ActivatedAt, Totales(carga.TotalsJson),
            Avisos(carga.ValidationSummaryJson));
    }

    /// <summary>Los totales guardados al aplicar (<c>TotalsJson</c>).</summary>
    public static IReadOnlyList<TotalDeCargaDto> Totales(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<TotalDeCargaDto>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<ErrorEntrePartes> Avisos(string? resumen)
    {
        if (string.IsNullOrWhiteSpace(resumen)) return [];
        try
        {
            using var doc = JsonDocument.Parse(resumen);
            return doc.RootElement.TryGetProperty("warnings", out var lista) && lista.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<ErrorEntrePartes>>(lista, Json) ?? []
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Un texto de fecha invariante para los mensajes.</summary>
    public static string Fecha(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
