using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination.CreditLimits;

// Feature 013, US2 (bloque 6B; T0406; FR-052, FR-060; api.md §7.2; data-model §2.12): los bloqueos y embargos del deudor. Impiden originar y
// vender a crédito; el cobro de lo vivo sigue. A mano sólo el manual y el embargo, con motivo; los automáticos los pone y los levanta la
// operación que los causa. (nuevo)

/// <summary>
/// El <b>lector único</b> de los bloqueos vigentes del deudor (feature 013, T0406; FR-060): lo consultan la originación
/// (<see cref="IBloqueosDelDeudor"/>), los cupos y la consulta de Comercial. Cuenta también los que la unidad de trabajo agregó y todavía no
/// guardó (la coincidencia en listas que se acaba de registrar en la misma operación). Sólo lee. (nuevo)
/// </summary>
public sealed class BloqueosDelDeudor(IApplicationDbContext db) : IBloqueosDelDeudor
{
    public async Task<IReadOnlyList<DebtorBlock>> VigentesAsync(IReadOnlyCollection<int> personIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(personIds);
        var ids = personIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        var guardados = await db.DebtorBlocks.AsNoTracking()
            .Where(b => ids.Contains(b.PersonId) && b.LiftedAt == null && !b.IsDeleted)
            .ToListAsync(ct);
        var vistos = guardados.Select(b => b.PublicId).ToHashSet();
        var sinGuardar = db.DebtorBlocks.Local.Where(b => ids.Contains(b.PersonId) && b.Vigente && !vistos.Contains(b.PublicId));
        return guardados.Concat(sinGuardar).OrderBy(b => b.StartedOn).ThenBy(b => b.Id).ToList();
    }

    /// <summary>Los vigentes de una persona con un motivo y un disparador (el UK filtrado de data-model §2.12).</summary>
    public async Task<DebtorBlock?> VigenteAsync(int personId, DebtorBlockReason motivo, string sourceKey, CancellationToken ct) =>
        (await VigentesAsync([personId], ct)).FirstOrDefault(b => b.Reason == motivo && string.Equals(b.SourceKey, sourceKey, StringComparison.Ordinal));
}

/// <summary>Un bloqueo como lo muestran las respuestas (api.md §7.2). (nuevo)</summary>
public sealed record DebtorBlockDto(
    Guid PublicId,
    PersonaRefDto Person,
    DebtorBlockReason Kind,
    string Origin,
    string? Reason,
    DateOnly Since,
    DateTime? LiftedAt,
    UsuarioRefDto? LiftedBy,
    string? LiftReason,
    Guid? Support,
    string HowItLifts,
    bool CanLiftManually = false);

/// <summary>Arma los DTO de bloqueos. (nuevo)</summary>
public static class ConsultasDeBloqueos
{
    public static async Task<IReadOnlyList<DebtorBlockDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<DebtorBlock> bloqueos, CancellationToken ct)
    {
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, bloqueos.Select(b => b.PersonId), ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, bloqueos.Where(b => b.LiftedByUserId is not null).Select(b => b.LiftedByUserId!.Value), ct);
        var deInsolvencia = bloqueos.Where(b => b.Reason == DebtorBlockReason.Insolvency).Select(b => b.PersonId).Distinct().ToList();
        var negociando = deInsolvencia.Count == 0
            ? new HashSet<int>()
            : (await db.InsolvencyProceedings.AsNoTracking()
                .Where(p => deInsolvencia.Contains(p.PersonId) && p.Status == InsolvencyProceedingStatus.Negotiating && !p.IsDeleted)
                .Select(p => p.PersonId).ToListAsync(ct)).ToHashSet();
        return bloqueos.Select(b => new DebtorBlockDto(b.PublicId, personas[b.PersonId], b.Reason, b.IsAutomatic ? "Automatic" : "Manual", b.Notes,
            b.StartedOn, b.LiftedAt, b.LiftedByUserId is { } u ? usuarios.GetValueOrDefault(u) : null, b.LiftReason, b.SupportAttachmentPublicId,
            ErroresDeDecisionesYCupos.ComoSeLevanta(b.Reason), b.Vigente && SeLevantaAMano(b, negociando.Contains(b.PersonId)))).ToList();
    }

    /// <summary>
    /// ¿Lo levanta una persona con <c>Lending.Blocks.Manage</c>? El manual y el embargo siempre; el de insolvencia cuando la persona ya no tiene una
    /// negociación abierta (decisión del dueño del 2026-10-15: el fracaso no lo levanta solo; el acuerdo aplicado como novedad sí).
    /// </summary>
    public static bool SeLevantaAMano(DebtorBlock b, bool negociacionAbierta) =>
        !b.IsAutomatic || (b.Reason == DebtorBlockReason.Insolvency && !negociacionAbierta);
}

/// <summary>
/// Registra un bloqueo manual o un embargo (api.md §7.2, <c>POST /api/lending/blocks</c>, <c>Lending.Blocks.Manage</c>, con clave y motivo):
/// sólo <c>Manual</c> y <c>Garnishment</c> (los demás los pone el sistema); uno vigente de la misma clase para la persona →
/// <c>Lending.Block.Duplicate</c>. El disparador de un manual es su propio <c>PublicId</c>. (nuevo)
/// </summary>
public sealed record RegisterDebtorBlockCommand(Guid PersonPublicId, DebtorBlockReason Kind, string Reason, Guid? SupportAttachmentPublicId = null)
    : IRequest<Result<DebtorBlockDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterDebtorBlockCommandValidator : ValidadorConMotivo<RegisterDebtorBlockCommand>
{
    public RegisterDebtorBlockCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.Kind).Must(DebtorBlock.EsManual)
            .WithMessage("A mano sólo se registra el bloqueo manual o el embargo: los demás los pone el sistema.");
    }
}

public sealed class RegisterDebtorBlockCommandHandler(
    IApplicationDbContext db,
    BloqueosDelDeudor bloqueos,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RegisterDebtorBlockCommand, Result<DebtorBlockDto>>
{
    public async Task<Result<DebtorBlockDto>> Handle(RegisterDebtorBlockCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<DebtorBlockDto>(ErroresDeGobierno.SinUsuario());
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Result.Failure<DebtorBlockDto>(ErroresDeGobierno.PersonaInexistente());

        var vigente = (await bloqueos.VigentesAsync([persona.Id], ct)).FirstOrDefault(b => b.Reason == request.Kind);
        if (vigente is not null)
            return Result.Failure<DebtorBlockDto>(ErroresDeDecisionesYCupos.BloqueoDuplicado(vigente.Reason, vigente.StartedOn, vigente.PublicId));

        var publicId = Guid.NewGuid();
        var bloqueo = DebtorBlock.Nuevo(persona.Id, request.Kind, publicId.ToString(), reloj.HoyLocal, request.Reason, usuario,
            soporte: request.SupportAttachmentPublicId);
        bloqueo.PublicId = publicId;
        db.DebtorBlocks.Add(bloqueo);
        await db.SaveChangesAsync(ct);
        return Result.Success((await ConsultasDeBloqueos.DtosAsync(db, [bloqueo], ct))[0]);
    }
}

/// <summary>
/// Levanta un bloqueo manual o un embargo (api.md §7.2, <c>POST /{id}/lift</c>, <c>Lending.Blocks.Manage</c>, con clave y motivo): el
/// levantamiento se escribe una sola vez (<c>[EscrituraUnica]</c>; <c>Lending.Block.AlreadyLifted</c>). Uno automático no se levanta a mano:
/// <c>Lending.Block.Automatic</c> con <c>{ kind, howItLifts }</c>; salvo el de insolvencia cuando la negociación ya no está abierta —la fracasada, cuyo
/// bloqueo se mantiene hasta que una persona autorizada lo levanta con motivo (decisión del dueño del 2026-10-15)—. (nuevo)
/// </summary>
public sealed record LiftDebtorBlockCommand(Guid BlockPublicId, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class LiftDebtorBlockCommandValidator : ValidadorConMotivo<LiftDebtorBlockCommand>
{
    public LiftDebtorBlockCommandValidator() => RuleFor(x => x.BlockPublicId).NotEmpty();
}

public sealed class LiftDebtorBlockCommandHandler(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj)
    : IRequestHandler<LiftDebtorBlockCommand, Result>
{
    public async Task<Result> Handle(LiftDebtorBlockCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure(ErroresDeGobierno.SinUsuario());
        var bloqueo = await db.DebtorBlocks.FirstOrDefaultAsync(b => b.PublicId == request.BlockPublicId && !b.IsDeleted, ct);
        if (bloqueo is null) return Result.Failure(ErroresDeDecisionesYCupos.BloqueoInexistente());
        var negociando = bloqueo.Reason == DebtorBlockReason.Insolvency && await db.InsolvencyProceedings.AsNoTracking()
            .AnyAsync(p => p.PersonId == bloqueo.PersonId && p.Status == InsolvencyProceedingStatus.Negotiating && !p.IsDeleted, ct);
        if (!ConsultasDeBloqueos.SeLevantaAMano(bloqueo, negociando)) return Result.Failure(ErroresDeDecisionesYCupos.BloqueoAutomatico(bloqueo.Reason));
        if (!bloqueo.Vigente)
            return Result.Failure(new ErrorConDatos(ErroresDeDecisionesYCupos.CodigoBloqueoYaLevantado, "El bloqueo ya se levantó.",
                new { liftedOn = bloqueo.LiftedOn is { } d ? ErroresDeOriginacion.Fecha(d) : null }));

        bloqueo.Levantar(reloj.HoyLocal, reloj.UtcNow, usuario, request.Reason);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Los bloqueos (api.md §7.2, <c>GET /api/lending/blocks</c>, <c>Lending.Loans.Read</c>): por persona, clase y vigencia, los más recientes
/// primero. Un bloqueo es de la persona, no de una oficina: no filtra por el alcance (exceptuado con motivo). (nuevo)
/// </summary>
public sealed record ListDebtorBlocksQuery(
    Guid? PersonPublicId = null,
    DebtorBlockReason? Kind = null,
    bool? Active = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<DebtorBlockDto>>>;

public sealed class ListDebtorBlocksQueryValidator : AbstractValidator<ListDebtorBlocksQuery>
{
    public ListDebtorBlocksQueryValidator() => RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
}

public sealed class ListDebtorBlocksQueryHandler(IApplicationDbContext db) : IRequestHandler<ListDebtorBlocksQuery, Result<PagedResult<DebtorBlockDto>>>
{
    public async Task<Result<PagedResult<DebtorBlockDto>>> Handle(ListDebtorBlocksQuery request, CancellationToken ct)
    {
        var consulta = db.DebtorBlocks.AsNoTracking().Where(b => !b.IsDeleted);
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(b => db.People.Any(p => p.Id == b.PersonId && p.PublicId == persona));
        if (request.Kind is { } clase) consulta = consulta.Where(b => b.Reason == clase);
        if (request.Active is true) consulta = consulta.Where(b => b.LiftedAt == null);
        if (request.Active is false) consulta = consulta.Where(b => b.LiftedAt != null);

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(b => b.StartedOn).ThenByDescending(b => b.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<DebtorBlockDto>(await ConsultasDeBloqueos.DtosAsync(db, filas, ct), pagina.SafePage, pagina.SafePageSize, total));
    }
}
