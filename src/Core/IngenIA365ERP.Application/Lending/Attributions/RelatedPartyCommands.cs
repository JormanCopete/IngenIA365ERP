using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Governance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Attributions;

// Feature 013, T0298 (US17; FR-040, T42, T63, T64; api.md §3.7; data-model §3.12): las personas vinculadas (Ley 454, art. 61). (nuevo)

/// <summary>Una persona con su documento: sólo en las respuestas <c>Read</c> (api.md §1.7).</summary>
public sealed record PersonaConDocumentoDto(Guid PersonPublicId, string FullName, string DocumentType, string DocumentNumber);

/// <summary>Un vínculo (api.md §3.7).</summary>
public sealed record RelatedPartyDto(
    Guid PublicId,
    PersonaConDocumentoDto Person,
    RelatedPartyBasis Basis,
    PersonaRefDto? LinkedTo,
    KinshipKind? KinshipKind,
    byte? KinshipDegree,
    decimal? ContributionPercentage,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    RelatedPartySource Source,
    string? Notes,
    string? EndReason);

/// <summary>
/// El único camino para registrar un vínculo, por pantalla o por plantilla (feature 013, T0298, T0302; plantillas.md §2): resuelve el
/// grado frente al límite nacional (<see cref="ReglasDeVinculados.ValidarGradoAsync"/>), hace que <b>lo registrado en Cartera prevalezca
/// sobre lo migrado</b> para la misma persona y fundamento (la vigencia migrada que se cruza termina la víspera o, si empezaba después,
/// se da de baja), valida con <see cref="RelatedParty.Validar"/> contra los vínculos vivos de la persona —también los aún sin guardar de
/// la misma importación— y lo agrega <b>sin guardar</b>. (nuevo)
/// </summary>
public sealed class AltaDeVinculados(IApplicationDbContext db, ReglasDeVinculados ley, IActorActual actorActual, IDateTimeService reloj)
{
    public const string MotivoDePrevalencia = "Prevalece el vínculo registrado en Cartera sobre el migrado.";

    public async Task<Result<RelatedParty>> RegistrarAsync(BorradorDeVinculado borrador, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(borrador);
        var grado = await ley.ValidarGradoAsync(borrador.KinshipDegree, borrador.ValidFrom, ct);
        if (grado.IsFailure) return Result.Failure<RelatedParty>(grado.Error);

        var guardados = await db.RelatedParties.Where(v => v.PersonId == borrador.PersonId && !v.IsDeleted).ToListAsync(ct);
        var existentes = guardados.Concat(db.RelatedParties.Local.Where(v => v.PersonId == borrador.PersonId && !v.IsDeleted && !guardados.Contains(v)))
            .Distinct().ToList();

        var migrados = existentes.Where(v => v.Source == RelatedPartySource.Migration && v.Basis == borrador.Basis
            && v.ValidFrom <= (borrador.ValidTo ?? DateOnly.MaxValue) && borrador.ValidFrom <= (v.ValidTo ?? DateOnly.MaxValue)).ToList();
        var restantes = existentes.Except(migrados).ToList();
        if (RelatedParty.Validar(borrador, restantes) is { } problema) return Result.Failure<RelatedParty>(ErroresDeGobierno.De(problema));

        var actor = await actorActual.ObtenerAsync(ct);
        foreach (var migrado in migrados)
        {
            if (migrado.ValidFrom < borrador.ValidFrom)
            {
                migrado.ValidTo = borrador.ValidFrom.AddDays(-1);
                migrado.EndedByUserId = actor.UserId;
                migrado.EndReason = MotivoDePrevalencia;
            }
            else
            {
                migrado.IsDeleted = true;
                migrado.DeletedAt = reloj.UtcNow;
                migrado.DeletedBy = actor.Name;
            }
        }

        var vinculo = RelatedParty.Registrar(borrador, restantes);
        db.RelatedParties.Add(vinculo);
        return Result.Success(vinculo);
    }
}

/// <summary>
/// Registra una persona vinculada (api.md §3.7, <c>POST /api/lending/related-parties</c>, <c>Lending.RelatedParties.Manage</c>). Antes de
/// la activación es de las pocas operaciones admitidas (FR-142). Cónyuge o pariente sin la persona del vínculo →
/// <c>Lending.RelatedParty.LinkRequired</c>; vigencias cruzadas del mismo vínculo → <c>.Duplicate</c>; grado por encima del límite legal
/// → <c>.KinshipDegreeExceeded</c>. <see cref="Support"/> es el soporte descrito (acta de elección, certificado, registro civil). (nuevo)
/// </summary>
public sealed record RegisterRelatedPartyCommand(
    Guid PersonPublicId,
    RelatedPartyBasis Basis,
    DateOnly ValidFrom,
    Guid? LinkedToPersonPublicId = null,
    KinshipKind? KinshipKind = null,
    byte? KinshipDegree = null,
    decimal? ContributionPercentage = null,
    DateOnly? ValidTo = null,
    string? Support = null,
    Guid? SupportAttachmentPublicId = null) : IRequest<Result<RelatedPartyDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterRelatedPartyCommandValidator : AbstractValidator<RegisterRelatedPartyCommand>
{
    public RegisterRelatedPartyCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Elegí la persona vinculada.");
        RuleFor(x => x.Basis).IsInEnum();
        RuleFor(x => x.KinshipKind).IsInEnum().When(x => x.KinshipKind is not null);
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige el vínculo.");
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("La vigencia no puede terminar antes de empezar.");
        RuleFor(x => x.ContributionPercentage!.Value).GreaterThan(0).LessThanOrEqualTo(1).PrecisionScale(9, 6, true)
            .WithMessage("El porcentaje de aportes es una fracción mayor que cero y hasta uno.").When(x => x.ContributionPercentage is not null);
        RuleFor(x => x.Support).MaximumLength(200);
    }
}

public sealed class RegisterRelatedPartyCommandHandler(IApplicationDbContext db, AltaDeVinculados alta)
    : IRequestHandler<RegisterRelatedPartyCommand, Result<RelatedPartyDto>>
{
    public async Task<Result<RelatedPartyDto>> Handle(RegisterRelatedPartyCommand request, CancellationToken ct)
    {
        var persona = await PersonaVivaAsync(db, request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<RelatedPartyDto>(ErroresDeGobierno.PersonaInexistente());
        int? relacionada = null;
        if (request.LinkedToPersonPublicId is { } p)
        {
            relacionada = await PersonaVivaAsync(db, p, ct);
            if (relacionada is null) return Result.Failure<RelatedPartyDto>(ErroresDeGobierno.PersonaInexistente());
        }

        var registro = await alta.RegistrarAsync(new BorradorDeVinculado(persona.Value, request.Basis, request.ValidFrom)
        {
            RelatedThroughPersonId = relacionada,
            KinshipKind = request.KinshipKind,
            KinshipDegree = request.KinshipDegree,
            ContributionPercentage = request.ContributionPercentage,
            ValidTo = request.ValidTo,
            SupportAttachmentPublicId = request.SupportAttachmentPublicId,
            Notes = request.Support,
        }, ct);
        if (registro.IsFailure) return Result.Failure<RelatedPartyDto>(registro.Error);

        await db.SaveChangesAsync(ct);
        return Result.Success((await ConsultasDeVinculados.DtosAsync(db, [registro.Value], ct))[0]);
    }

    internal static Task<int?> PersonaVivaAsync(IApplicationDbContext db, Guid publicId, CancellationToken ct) =>
        db.People.AsNoTracking().Where(p => p.PublicId == publicId && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
}

/// <summary>Termina un vínculo (api.md §3.7, <c>POST /{id}/end</c>, 204): cierra la vigencia con quién y por qué; no se borra. (nuevo)</summary>
public sealed record EndRelatedPartyCommand(Guid RelatedPartyPublicId, DateOnly ValidTo, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class EndRelatedPartyCommandValidator : ValidadorConMotivo<EndRelatedPartyCommand>
{
    public EndRelatedPartyCommandValidator()
    {
        RuleFor(x => x.RelatedPartyPublicId).NotEmpty();
        RuleFor(x => x.ValidTo).NotEqual(default(DateOnly)).WithMessage("Indicá hasta cuándo rige el vínculo.");
        RuleFor(x => x.Reason).MaximumLength(300).WithMessage("El motivo admite hasta 300 caracteres.");
    }
}

public sealed class EndRelatedPartyCommandHandler(IApplicationDbContext db, IActorActual actorActual) : IRequestHandler<EndRelatedPartyCommand, Result>
{
    public async Task<Result> Handle(EndRelatedPartyCommand request, CancellationToken ct)
    {
        var vinculo = await db.RelatedParties.FirstOrDefaultAsync(v => v.PublicId == request.RelatedPartyPublicId && !v.IsDeleted, ct);
        if (vinculo is null) return Result.Failure(ErroresDeGobierno.VinculadoInexistente());
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure(ErroresDeGobierno.SinUsuario());

        if (vinculo.Terminar(request.ValidTo, usuario, request.Reason) is { } problema) return Result.Failure(ErroresDeGobierno.De(problema));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Las personas vinculadas (api.md §3.7, <c>GET /api/lending/related-parties?personPublicId=&amp;basis=&amp;date=&amp;page=</c>,
/// <c>Lending.RelatedParties.Read</c>), por documento y fecha. Es <b>lectura sensible</b>: cada consulta deja
/// <c>Lending.RelatedParty.Viewed</c> con los filtros y las filas por <see cref="LendingAuditEmitter"/> (C89). Con <see cref="Date"/>,
/// sólo los vigentes ese día. Los vínculos son de personas, no de una oficina: no aplica el alcance de oficina y línea. (nuevo)
/// </summary>
public sealed record ListRelatedPartiesQuery(
    Guid? PersonPublicId = null,
    RelatedPartyBasis? Basis = null,
    DateOnly? Date = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<RelatedPartyDto>>>;

public sealed class ListRelatedPartiesQueryValidator : AbstractValidator<ListRelatedPartiesQuery>;

public sealed class ListRelatedPartiesQueryHandler(IApplicationDbContext db, LendingAuditEmitter auditoria)
    : IRequestHandler<ListRelatedPartiesQuery, Result<PagedResult<RelatedPartyDto>>>
{
    public async Task<Result<PagedResult<RelatedPartyDto>>> Handle(ListRelatedPartiesQuery request, CancellationToken ct)
    {
        var consulta = db.RelatedParties.AsNoTracking().Where(v => !v.IsDeleted);
        if (request.PersonPublicId is { } persona)
        {
            var id = await db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(v => v.PersonId == id || v.RelatedThroughPersonId == id);
        }
        if (request.Basis is { } fundamento) consulta = consulta.Where(v => v.Basis == fundamento);
        if (request.Date is { } fecha) consulta = consulta.Where(v => v.ValidFrom <= fecha && (v.ValidTo == null || v.ValidTo >= fecha));

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta
            .Join(db.People.AsNoTracking().IgnoreQueryFilters(), v => v.PersonId, p => p.Id, (v, p) => new { v, p.TaxId })
            .OrderBy(x => x.TaxId).ThenBy(x => x.v.ValidFrom).ThenBy(x => x.v.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .Select(x => x.v)
            .ToListAsync(ct);
        var items = await ConsultasDeVinculados.DtosAsync(db, filas, ct);

        await auditoria.EmitirVinculadosVistosAsync(new
        {
            personPublicId = request.PersonPublicId, basis = request.Basis?.ToString(), date = request.Date?.ToString("yyyy-MM-dd"),
            page = pagina.SafePage,
        }, items.Count, ct);
        return Result.Success(new PagedResult<RelatedPartyDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Lo que comparten las consultas y los comandos de vinculados. (nuevo)</summary>
internal static class ConsultasDeVinculados
{
    public static async Task<IReadOnlyList<RelatedPartyDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<RelatedParty> filas, CancellationToken ct)
    {
        var ids = filas.Select(f => f.PersonId).Concat(filas.Where(f => f.RelatedThroughPersonId is not null).Select(f => f.RelatedThroughPersonId!.Value))
            .Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        return filas.Select(f =>
        {
            var p = personas[f.PersonId];
            var relacionada = f.RelatedThroughPersonId is { } r ? personas[r] : null;
            return new RelatedPartyDto(f.PublicId, new PersonaConDocumentoDto(p.PublicId, ReferenciasDeGobierno.Nombre(p), p.IdType, p.TaxId), f.Basis,
                relacionada is null ? null : new PersonaRefDto(relacionada.PublicId, ReferenciasDeGobierno.Nombre(relacionada)),
                f.KinshipKind, f.KinshipDegree, f.ContributionPercentage, f.ValidFrom, f.ValidTo, f.Source, f.Notes, f.EndReason);
        }).ToList();
    }
}
