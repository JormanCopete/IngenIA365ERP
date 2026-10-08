using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Governance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Sessions;

// Feature 013, T0294 (US17; T64, FR-039, FR-040, F7; api.md §3.6; data-model §3.5–§3.7): los órganos de decisión, sus reglas de quórum y
// mayorías con vigencia y sus miembros. (nuevo)

/// <summary>Una fracción exacta de quórum o de mayoría, con su comparación (la de vinculados siempre es «al menos»).</summary>
public sealed record FraccionInput(int Numerator, int Denominator, FractionComparison Comparison = FractionComparison.MoreThan);

/// <summary>
/// La regla de quórum y mayorías que se registra (api.md §3.6): quórum sobre la composición, mayoría ordinaria con su base y, en el
/// consejo, la fracción calificada de vinculados sobre la composición.
/// </summary>
public sealed record ReglasDeOrganoInput(
    FraccionInput Quorum,
    FraccionInput OrdinaryMajority,
    FraccionInput? RelatedPartyFraction,
    DateOnly ValidFrom,
    string Reason,
    string Source,
    MajorityBase MajorityBase = MajorityBase.PresentEligible)
{
    public BorradorDeReglasDeOrgano Borrador() => new(ValidFrom, Quorum.Numerator, Quorum.Denominator, Quorum.Comparison,
        OrdinaryMajority.Numerator, OrdinaryMajority.Denominator, OrdinaryMajority.Comparison, MajorityBase,
        RelatedPartyFraction?.Numerator, RelatedPartyFraction?.Denominator, Source, Reason);
}

public sealed record FraccionDto(int Numerator, int Denominator, FractionComparison Comparison);

/// <summary>Una versión de las reglas del órgano.</summary>
public sealed record GoverningBodyRulesDto(
    int Version,
    FraccionDto Quorum,
    FraccionDto OrdinaryMajority,
    MajorityBase MajorityBase,
    FraccionDto? RelatedPartyFraction,
    DateOnly ValidFrom,
    DateTime RegisteredAt,
    string Source,
    string Reason);

/// <summary>Un órgano en la lista (api.md §3.6): sólo códigos, nombres y conteos (C84).</summary>
public sealed record GoverningBodyListItemDto(
    Guid PublicId,
    string Code,
    string Name,
    GoverningBodyKind Kind,
    bool DecidesCredit,
    string MinutesPrefix,
    string NextMinutesNumber,
    GoverningBodyRulesDto? CurrentRules,
    int Members,
    bool IsActive);

/// <summary>Un miembro: la persona por su nombre, su papel, si vota y su vigencia (C84: ni documento ni contacto personal).</summary>
public sealed record GoverningBodyMemberDto(
    Guid MemberPublicId,
    PersonaRefDto Person,
    BodyMemberRole Role,
    bool Votes,
    bool IsPrincipal,
    Guid? AlternateForMemberPublicId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? AppointmentNotes);

/// <summary>Un órgano con sus miembros y la historia de sus reglas (api.md §3.6, <c>GET /{id}</c>).</summary>
public sealed record GoverningBodyDto(
    Guid PublicId,
    string Code,
    string Name,
    GoverningBodyKind Kind,
    bool DecidesCredit,
    string MinutesPrefix,
    string NextMinutesNumber,
    GoverningBodyRulesDto? CurrentRules,
    bool IsActive,
    IReadOnlyList<GoverningBodyMemberDto> MemberList,
    IReadOnlyList<GoverningBodyRulesDto> RulesHistory);

/// <summary>
/// Crea un órgano con su primera versión de reglas (api.md §3.6, <c>POST /api/lending/governing-bodies</c>,
/// <c>Lending.GoverningBodies.Manage</c>). El código es la nomenclatura de la cooperativa (<see cref="CodigoDeCatalogo"/>, hasta 10). (nuevo)
/// </summary>
public sealed record CreateGoverningBodyCommand(
    string Code,
    string Name,
    GoverningBodyKind Kind,
    bool DecidesCredit,
    string MinutesPrefix,
    ReglasDeOrganoInput Rules) : IRequest<Result<GoverningBodyDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateGoverningBodyCommandValidator : AbstractValidator<CreateGoverningBodyCommand>
{
    public CreateGoverningBodyCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Indicá el código del órgano.")
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage("El código admite hasta 10 caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indicá el nombre del órgano.").MaximumLength(120);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.MinutesPrefix).NotEmpty().WithMessage("Indicá el prefijo de las actas.").MaximumLength(10);
        RuleFor(x => x.Rules).NotNull().WithMessage("El órgano nace con sus reglas de quórum y mayorías.");
        RuleFor(x => x.Rules).SetValidator(new ReglasDeOrganoInputValidator()!).When(x => x.Rules is not null);
    }
}

/// <summary>La forma de una regla: fuente y motivo, y la fecha.</summary>
public sealed class ReglasDeOrganoInputValidator : AbstractValidator<ReglasDeOrganoInput>
{
    public ReglasDeOrganoInputValidator()
    {
        RuleFor(x => x.Quorum).NotNull();
        RuleFor(x => x.OrdinaryMajority).NotNull();
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige la regla.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indicá el motivo.").MaximumLength(300);
        RuleFor(x => x.Source).NotEmpty().WithMessage("Indicá la fuente (estatutos o reglamento).").MaximumLength(300);
        RuleFor(x => x.MajorityBase).IsInEnum();
    }
}

/// <summary>
/// Código vivo repetido → <c>Catalogo.CodigoDuplicado</c> con el nombre del existente; un segundo consejo activo →
/// <c>Lending.GoverningBody.BoardAlreadyActive</c>; la regla se valida en su forma (<c>RulesInvalid</c>), contra hoy
/// (<c>ValidFromInPast</c>) y contra la ley (<see cref="ReglasDeVinculados"/>). Órgano y regla en una transacción.
/// </summary>
public sealed class CreateGoverningBodyCommandHandler(IApplicationDbContext db, ReglasDeVinculados ley, IDateTimeService reloj)
    : IRequestHandler<CreateGoverningBodyCommand, Result<GoverningBodyDto>>
{
    public async Task<Result<GoverningBodyDto>> Handle(CreateGoverningBodyCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var repetido = await db.GoverningBodies.AsNoTracking().FirstOrDefaultAsync(b => b.Code == codigo && !b.IsDeleted, ct);
        if (repetido is not null)
            return Result.Failure<GoverningBodyDto>(CodigoDeCatalogo.Duplicado("un órgano", codigo, repetido.Name, repetido.PublicId));

        var organo = new GoverningBody
        {
            Code = codigo,
            Name = request.Name.Trim(),
            Kind = request.Kind,
            DecidesCredit = request.DecidesCredit,
            MinutesPrefix = request.MinutesPrefix.Trim().ToUpperInvariant(),
            IsActive = true,
        };
        var activos = await db.GoverningBodies.AsNoTracking().Where(b => b.IsActive && !b.IsDeleted).ToListAsync(ct);
        if (organo.ValidarConsejoUnico(activos) is { } consejo) return Result.Failure<GoverningBodyDto>(ErroresDeGobierno.De(consejo));

        var reglas = request.Rules.Borrador();
        var validas = await ConsultasDeOrganos.ValidarReglasAsync(reglas, request.Kind, ley, reloj.HoyLocal, ct);
        if (validas.IsFailure) return Result.Failure<GoverningBodyDto>(validas.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            db.GoverningBodies.Add(organo);
            await db.SaveChangesAsync(ct);
            db.GoverningBodyRuleVersions.Add(GoverningBodyRuleVersion.Registrar(organo.Id, organo.Kind, 1, reglas, reloj.UtcNow));
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeOrganos.DtoAsync(db, organo, reloj.HoyLocal, reloj.UtcNow, ct));
        }, ct);
    }
}

/// <summary>
/// Registra una versión nueva de las reglas de quórum y mayorías (api.md §3.6, <c>POST /{id}/rules</c>): un hecho nuevo, la anterior
/// rige hasta la víspera (lectura bitemporal). Con actividad financiera, la fracción de vinculados bajo el límite nacional →
/// <c>Lending.Parameter.BelowLegalFloor</c>. (nuevo)
/// </summary>
public sealed record AddGoverningBodyRulesVersionCommand(Guid GoverningBodyPublicId, ReglasDeOrganoInput Rules)
    : IRequest<Result<GoverningBodyRulesDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    public string Reason => Rules?.Reason ?? string.Empty;
}

public sealed class AddGoverningBodyRulesVersionCommandValidator : AbstractValidator<AddGoverningBodyRulesVersionCommand>
{
    public AddGoverningBodyRulesVersionCommandValidator()
    {
        RuleFor(x => x.GoverningBodyPublicId).NotEmpty();
        RuleFor(x => x.Rules).NotNull().SetValidator(new ReglasDeOrganoInputValidator()!);
    }
}

public sealed class AddGoverningBodyRulesVersionCommandHandler(IApplicationDbContext db, ReglasDeVinculados ley, IDateTimeService reloj)
    : IRequestHandler<AddGoverningBodyRulesVersionCommand, Result<GoverningBodyRulesDto>>
{
    public async Task<Result<GoverningBodyRulesDto>> Handle(AddGoverningBodyRulesVersionCommand request, CancellationToken ct)
    {
        var organo = await db.GoverningBodies.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.GoverningBodyPublicId && !b.IsDeleted, ct);
        if (organo is null) return Result.Failure<GoverningBodyRulesDto>(ErroresDeGobierno.OrganoInexistente());

        var reglas = request.Rules.Borrador();
        var validas = await ConsultasDeOrganos.ValidarReglasAsync(reglas, organo.Kind, ley, reloj.HoyLocal, ct);
        if (validas.IsFailure) return Result.Failure<GoverningBodyRulesDto>(validas.Error);

        var ultima = await db.GoverningBodyRuleVersions.IgnoreQueryFilters().Where(v => v.GoverningBodyId == organo.Id)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
        var version = GoverningBodyRuleVersion.Registrar(organo.Id, organo.Kind, ultima + 1, reglas, reloj.UtcNow);
        db.GoverningBodyRuleVersions.Add(version);
        await db.SaveChangesAsync(ct);
        return Result.Success(ConsultasDeOrganos.Reglas(version));
    }
}

/// <summary>
/// Nombra a una persona del maestro como miembro del órgano (api.md §3.6, <c>POST /{id}/members</c>). Los miembros son personas, no
/// necesariamente usuarios (I-1). Persona inexistente o eliminada → <c>Core.Person.NotFound</c>; vigencias cruzadas de la misma persona →
/// <c>Lending.GoverningBody.MemberOverlaps</c>; un suplente nombra a un principal vigente del mismo órgano
/// (<c>Lending.GoverningBody.AlternateInvalid</c>). (nuevo)
/// </summary>
public sealed record AddGoverningBodyMemberCommand(
    Guid GoverningBodyPublicId,
    Guid PersonPublicId,
    BodyMemberRole Position,
    bool Votes,
    DateOnly ValidFrom,
    Guid? AlternateForMemberPublicId = null,
    string? AppointmentNotes = null,
    Guid? AppointmentAttachmentPublicId = null) : IRequest<Result<GoverningBodyMemberDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AddGoverningBodyMemberCommandValidator : AbstractValidator<AddGoverningBodyMemberCommand>
{
    public AddGoverningBodyMemberCommandValidator()
    {
        RuleFor(x => x.GoverningBodyPublicId).NotEmpty();
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Elegí la persona.");
        RuleFor(x => x.Position).IsInEnum();
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo es miembro.");
        RuleFor(x => x.AppointmentNotes).MaximumLength(300);
        RuleFor(x => x.AlternateForMemberPublicId).NotNull().WithMessage("Un suplente nombra al principal del que es suplente.")
            .When(x => x.Position == BodyMemberRole.Alternate);
    }
}

public sealed class AddGoverningBodyMemberCommandHandler(IApplicationDbContext db)
    : IRequestHandler<AddGoverningBodyMemberCommand, Result<GoverningBodyMemberDto>>
{
    public async Task<Result<GoverningBodyMemberDto>> Handle(AddGoverningBodyMemberCommand request, CancellationToken ct)
    {
        var organo = await db.GoverningBodies.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.GoverningBodyPublicId && !b.IsDeleted, ct);
        if (organo is null) return Result.Failure<GoverningBodyMemberDto>(ErroresDeGobierno.OrganoInexistente());

        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Result.Failure<GoverningBodyMemberDto>(ErroresDeGobierno.PersonaInexistente());

        var miembros = await db.GoverningBodyMembers.Where(m => m.GoverningBodyId == organo.Id && !m.IsDeleted).ToListAsync(ct);
        GoverningBodyMember? principal = null;
        if (request.AlternateForMemberPublicId is { } principalPublico)
        {
            principal = miembros.FirstOrDefault(m => m.PublicId == principalPublico);
            if (principal is null) return Result.Failure<GoverningBodyMemberDto>(ErroresDeGobierno.MiembroInexistente());
        }

        var borrador = new BorradorDeMiembro(organo.Id, persona.Id, request.Position, request.Votes,
            request.Position != BodyMemberRole.Alternate, principal?.Id, request.ValidFrom)
        {
            AppointmentNotes = request.AppointmentNotes,
            AppointmentAttachmentPublicId = request.AppointmentAttachmentPublicId,
        };
        if (GoverningBodyMember.Validar(borrador, miembros, principal) is { } problema)
            return Result.Failure<GoverningBodyMemberDto>(ErroresDeGobierno.De(problema));

        var miembro = GoverningBodyMember.Nombrar(borrador, miembros, principal);
        db.GoverningBodyMembers.Add(miembro);
        await db.SaveChangesAsync(ct);
        return Result.Success(new GoverningBodyMemberDto(miembro.PublicId, new PersonaRefDto(persona.PublicId, ReferenciasDeGobierno.Nombre(persona)),
            miembro.Role, miembro.HasVote, miembro.IsPrincipal, principal?.PublicId, miembro.ValidFrom, miembro.ValidTo, miembro.AppointmentNotes));
    }
}

/// <summary>Termina la vigencia de un miembro (api.md §3.6, <c>POST /{id}/members/{memberId}/end</c>, 204). No se borra. (nuevo)</summary>
public sealed record EndGoverningBodyMemberCommand(Guid GoverningBodyPublicId, Guid MemberPublicId, DateOnly ValidTo, string Reason)
    : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class EndGoverningBodyMemberCommandValidator : ValidadorConMotivo<EndGoverningBodyMemberCommand>
{
    public EndGoverningBodyMemberCommandValidator()
    {
        RuleFor(x => x.GoverningBodyPublicId).NotEmpty();
        RuleFor(x => x.MemberPublicId).NotEmpty();
        RuleFor(x => x.ValidTo).NotEqual(default(DateOnly)).WithMessage("Indicá hasta cuándo es miembro.");
    }
}

public sealed class EndGoverningBodyMemberCommandHandler(IApplicationDbContext db) : IRequestHandler<EndGoverningBodyMemberCommand, Result>
{
    public async Task<Result> Handle(EndGoverningBodyMemberCommand request, CancellationToken ct)
    {
        var miembro = await (from m in db.GoverningBodyMembers
                             join b in db.GoverningBodies on m.GoverningBodyId equals b.Id
                             where m.PublicId == request.MemberPublicId && b.PublicId == request.GoverningBodyPublicId && !m.IsDeleted && !b.IsDeleted
                             select m).FirstOrDefaultAsync(ct);
        if (miembro is null) return Result.Failure(ErroresDeGobierno.MiembroInexistente());

        if (miembro.Terminar(request.ValidTo) is { } problema) return Result.Failure(ErroresDeGobierno.De(problema));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Desactiva un órgano (api.md §3.6, <c>POST /{id}/deactivate</c>, 204): no se borra. Si un nivel de la matriz vigente hoy o de una
/// versión que empieza después lo nombra → <c>Lending.GoverningBody.InUse</c> con <c>data.matrixVersion</c>. (nuevo)
/// </summary>
public sealed record DeactivateGoverningBodyCommand(Guid GoverningBodyPublicId, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class DeactivateGoverningBodyCommandValidator : ValidadorConMotivo<DeactivateGoverningBodyCommand>
{
    public DeactivateGoverningBodyCommandValidator() => RuleFor(x => x.GoverningBodyPublicId).NotEmpty();
}

public sealed class DeactivateGoverningBodyCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<DeactivateGoverningBodyCommand, Result>
{
    public async Task<Result> Handle(DeactivateGoverningBodyCommand request, CancellationToken ct)
    {
        var organo = await db.GoverningBodies.FirstOrDefaultAsync(b => b.PublicId == request.GoverningBodyPublicId && !b.IsDeleted, ct);
        if (organo is null) return Result.Failure(ErroresDeGobierno.OrganoInexistente());

        var hoy = reloj.HoyLocal;
        var versiones = await db.AttributionMatrices.AsNoTracking().Where(m => !m.IsDeleted).ToListAsync(ct);
        var vigente = VigenciaBitemporal.VigenteDeCartera(versiones, hoy, reloj.UtcNow);
        var queRigen = versiones.Where(v => v == vigente || v.ValidFrom > hoy).Select(v => v.Id).ToList();
        var enUso = await db.AttributionLevels.AsNoTracking()
            .Where(n => n.GoverningBodyId == organo.Id && queRigen.Contains(n.MatrixId))
            .Select(n => n.MatrixId)
            .ToListAsync(ct);
        if (enUso.Count > 0)
            return Result.Failure(ErroresDeGobierno.OrganoEnUso(versiones.Where(v => enUso.Contains(v.Id)).Min(v => v.VersionNumber)));

        organo.Desactivar();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Los órganos (api.md §3.6, <c>GET /api/lending/governing-bodies?includeInactive=</c>), por código. Catálogo sin oficina. (nuevo)</summary>
public sealed record ListGoverningBodiesQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<GoverningBodyListItemDto>>>;

public sealed class ListGoverningBodiesQueryValidator : AbstractValidator<ListGoverningBodiesQuery>;

public sealed class ListGoverningBodiesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListGoverningBodiesQuery, Result<IReadOnlyList<GoverningBodyListItemDto>>>
{
    public async Task<Result<IReadOnlyList<GoverningBodyListItemDto>>> Handle(ListGoverningBodiesQuery request, CancellationToken ct)
    {
        var organos = await db.GoverningBodies.AsNoTracking().Where(b => !b.IsDeleted && (request.IncludeInactive || b.IsActive))
            .OrderBy(b => b.Code).ToListAsync(ct);
        var lista = new List<GoverningBodyListItemDto>(organos.Count);
        foreach (var o in organos)
        {
            var d = await ConsultasDeOrganos.DtoAsync(db, o, reloj.HoyLocal, reloj.UtcNow, ct);
            lista.Add(new GoverningBodyListItemDto(d.PublicId, d.Code, d.Name, d.Kind, d.DecidesCredit, d.MinutesPrefix, d.NextMinutesNumber,
                d.CurrentRules, d.MemberList.Count(m => m.ValidFrom <= reloj.HoyLocal && (m.ValidTo is null || m.ValidTo >= reloj.HoyLocal)), d.IsActive));
        }
        return Result.Success<IReadOnlyList<GoverningBodyListItemDto>>(lista);
    }
}

/// <summary>Un órgano con sus miembros y la historia de sus reglas (api.md §3.6, <c>GET /{id}?date=</c>; reglas vigentes en esa fecha). (nuevo)</summary>
public sealed record GetGoverningBodyQuery(Guid GoverningBodyPublicId, DateOnly? Date = null) : IRequest<Result<GoverningBodyDto>>;

public sealed class GetGoverningBodyQueryValidator : AbstractValidator<GetGoverningBodyQuery>
{
    public GetGoverningBodyQueryValidator() => RuleFor(x => x.GoverningBodyPublicId).NotEmpty();
}

public sealed class GetGoverningBodyQueryHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<GetGoverningBodyQuery, Result<GoverningBodyDto>>
{
    public async Task<Result<GoverningBodyDto>> Handle(GetGoverningBodyQuery request, CancellationToken ct)
    {
        var organo = await db.GoverningBodies.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.GoverningBodyPublicId && !b.IsDeleted, ct);
        return organo is null
            ? Result.Failure<GoverningBodyDto>(ErroresDeGobierno.OrganoInexistente())
            : Result.Success(await ConsultasDeOrganos.DtoAsync(db, organo, request.Date ?? reloj.HoyLocal, reloj.UtcNow, ct));
    }
}

/// <summary>Lo que comparten los comandos y las consultas de órganos. (nuevo)</summary>
internal static class ConsultasDeOrganos
{
    /// <summary>Forma, fecha (no antes de hoy) y ley.</summary>
    public static async Task<Result> ValidarReglasAsync(BorradorDeReglasDeOrgano reglas, GoverningBodyKind clase, ReglasDeVinculados ley, DateOnly hoy,
        CancellationToken ct)
    {
        if (GoverningBodyRuleVersion.Validar(reglas, clase) is { } problema) return Result.Failure(ErroresDeGobierno.De(problema));
        if (reglas.ValidFrom < hoy)
            return Result.Failure(ErroresDeGobierno.ReglasInvalidas("ValidFromInPast",
                $"La regla no puede regir desde {reglas.ValidFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}, antes de hoy " +
                $"({hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})."));
        return await ley.ValidarAsync(clase, reglas, ct);
    }

    public static GoverningBodyRulesDto Reglas(GoverningBodyRuleVersion v) => new(v.VersionNumber,
        new FraccionDto(v.QuorumNumerator, v.QuorumDenominator, v.QuorumComparison),
        new FraccionDto(v.MajorityNumerator, v.MajorityDenominator, v.MajorityComparison), v.MajorityBase,
        v.RelatedPartyNumerator is { } n && v.RelatedPartyDenominator is { } d ? new FraccionDto(n, d, FractionComparison.AtLeast) : null,
        v.ValidFrom, v.RegisteredAt, v.Source, v.Reason);

    /// <summary>El número que llevaría la próxima acta del órgano (no lo consume: lo asigna el cierre).</summary>
    public static async Task<string> SiguienteActaAsync(IApplicationDbContext db, GoverningBody o, CancellationToken ct)
    {
        var ambito = NumeradorDeCartera.AmbitoDeOrgano(o);
        var fila = await db.LendingNumberSequences.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Kind == LendingNumberKind.SessionMinutes && s.ScopeKey == ambito, ct);
        return fila is null
            ? NumeradorDeCartera.Formatear(o.MinutesPrefix, 1, NumeradorDeCartera.DigitosPorDefecto)
            : NumeradorDeCartera.Formatear(fila.Prefix, fila.NextValue, fila.MinDigits);
    }

    public static async Task<GoverningBodyDto> DtoAsync(IApplicationDbContext db, GoverningBody o, DateOnly fecha, DateTime ahora, CancellationToken ct)
    {
        var reglas = await db.GoverningBodyRuleVersions.AsNoTracking().Where(v => v.GoverningBodyId == o.Id && !v.IsDeleted).ToListAsync(ct);
        var vigente = VigenciaBitemporal.VigenteDeCartera(reglas, fecha, ahora);
        var miembros = await db.GoverningBodyMembers.AsNoTracking().Where(m => m.GoverningBodyId == o.Id && !m.IsDeleted)
            .OrderBy(m => m.ValidFrom).ThenBy(m => m.Id).ToListAsync(ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, miembros.Select(m => m.PersonId), ct);
        var publicoDeMiembro = miembros.ToDictionary(m => m.Id, m => m.PublicId);

        return new GoverningBodyDto(o.PublicId, o.Code, o.Name, o.Kind, o.DecidesCredit, o.MinutesPrefix, await SiguienteActaAsync(db, o, ct),
            vigente is null ? null : Reglas(vigente), o.IsActive,
            miembros.Select(m => new GoverningBodyMemberDto(m.PublicId, personas[m.PersonId], m.Role, m.HasVote, m.IsPrincipal,
                m.AlternateForMemberId is { } a ? publicoDeMiembro.GetValueOrDefault(a) : null, m.ValidFrom, m.ValidTo, m.AppointmentNotes)).ToList(),
            reglas.OrderByDescending(v => v.ValidFrom).ThenByDescending(v => v.VersionNumber).Select(Reglas).ToList());
    }
}
