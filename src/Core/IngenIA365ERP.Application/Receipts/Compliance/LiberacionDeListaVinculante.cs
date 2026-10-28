using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Compliance;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Cumplimiento;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Compliance;

// Feature 015, segunda ronda del 2026-10-27 (D-e; FR-230): el falso positivo de lista vinculante en la ventanilla. La operación con coincidencia exacta nace Blocked y
// no tiene salida por la caja; la libera el oficial de cumplimiento desde su bandeja, por operación, con motivo y soporte, y queda una fila por persona liberada en
// RCD_ScreeningReleases. El cajero de la sesión no la libera aunque tenga el permiso. La confirmación y la retención ya no consultan otra vez a esas personas en esa
// operación (LiberacionesDeLista.PersonasLiberadasAsync); una persona nueva que coincida la detiene como siempre.

/// <summary>Una persona que coincidió en lista vinculante en una operación bloqueada. (nuevo)</summary>
public sealed record PersonaEnListaDto(Guid PersonPublicId, string Name);

/// <summary>Una operación de caja bloqueada por lista vinculante, para la bandeja del oficial. (nuevo)</summary>
public sealed record BlockedOperationDto(Guid OperationPublicId, Guid SessionPublicId, string? CashierName, Guid PayerPersonPublicId, string PayerName,
    DateTime CreatedAt, IReadOnlyList<PersonaEnListaDto> Matches);

/// <summary>Lo común de las liberaciones de lista vinculante. (nuevo)</summary>
public static class LiberacionesDeLista
{
    /// <summary>Las personas ya liberadas de una operación (<c>COR_People.Id</c>).</summary>
    public static async Task<IReadOnlySet<int>> PersonasLiberadasAsync(IApplicationDbContext db, int operacionId, CancellationToken ct) =>
        (await db.ScreeningReleases.AsNoTracking().Where(r => r.CashOperationId == operacionId).Select(r => r.PersonId).ToListAsync(ct)).ToHashSet();

    /// <summary>Las personas de la operación que coincidieron en lista vinculante (la bitácora de señales), sin las ya liberadas.</summary>
    public static async Task<IReadOnlyList<int>> CoincidenciasAsync(IApplicationDbContext db, Guid operacionPublicId, CancellationToken ct) =>
        await db.ReceiptsComplianceAlertLogs.AsNoTracking()
            .Where(l => l.DocumentPublicId == operacionPublicId && l.Signal == SenalDeEfectivo.BindingListMatch && l.PersonId != null)
            .Select(l => l.PersonId!.Value).Distinct().ToListAsync(ct);

    public static Error NoBloqueada() => new("Receipts.Compliance.NotBlocked", "La operación no está bloqueada por una lista vinculante.");

    public static Error PorElCajero() => new("Receipts.Compliance.ReleaseByCashier",
        "El cajero que atiende la operación no la libera: la libera el oficial de cumplimiento.");

    public static Error SoporteInvalido() => new("Receipts.Compliance.ReleaseSupportInvalid",
        "Adjunte el soporte de la liberación a la operación (tipo «Soporte de la liberación de lista») y espere a que quede disponible.");
}

/// <summary><c>GET /api/receipts/compliance/blocked</c> (<c>Receipts.Compliance.View</c> o <c>.ReleaseBindingMatch</c>): las operaciones bloqueadas por lista vinculante. (nuevo)</summary>
public sealed record ListBlockedOperationsQuery : IRequest<Result<IReadOnlyList<BlockedOperationDto>>>;

public sealed class ListBlockedOperationsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListBlockedOperationsQuery, Result<IReadOnlyList<BlockedOperationDto>>>
{
    public async Task<Result<IReadOnlyList<BlockedOperationDto>>> Handle(ListBlockedOperationsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var filas = await (from o in db.CashOperations.AsNoTracking()
                           join s in db.ReceiptsCashSessions.AsNoTracking() on o.CashSessionId equals s.Id
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on o.PayerPersonId equals p.Id
                           where o.Status == CashOperationState.Blocked
                           orderby o.Id descending
                           select new { o.PublicId, Sesion = s.PublicId, s.CashBoxId, s.CashierUserId, Pagador = p.PublicId, p.FirstName, p.OtherNames, p.LastName,
                               p.SecondLastName, p.BusinessName, o.CreatedAt }).Take(300).ToListAsync(ct);
        var cajas = filas.Select(f => f.CashBoxId).Distinct().ToList();
        var puntos = await db.CashBoxes.AsNoTracking().Where(c => cajas.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.CollectionPointId, ct);
        var visibles = filas.Where(f => mio.IncluyeCaja(f.CashBoxId, puntos.GetValueOrDefault(f.CashBoxId))).ToList();
        var cajeros = visibles.Select(f => f.CashierUserId).Distinct().ToList();
        var nombres = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => cajeros.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);
        var lista = new List<BlockedOperationDto>(visibles.Count);
        foreach (var f in visibles)
        {
            var ids = await LiberacionesDeLista.CoincidenciasAsync(db, f.PublicId, ct);
            var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
                .Select(p => new { p.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName }).ToListAsync(ct);
            lista.Add(new BlockedOperationDto(f.PublicId, f.Sesion, nombres.GetValueOrDefault(f.CashierUserId), f.Pagador,
                PersonFactory.NombreVisible(f.FirstName, f.OtherNames, f.LastName, f.SecondLastName, f.BusinessName), f.CreatedAt,
                personas.Select(p => new PersonaEnListaDto(p.PublicId,
                    PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName))).ToList()));
        }
        return Result.Success<IReadOnlyList<BlockedOperationDto>>(lista);
    }
}

/// <summary>
/// <c>POST /api/receipts/cash-operations/{id}/release-binding-match</c> (<c>Receipts.Compliance.ReleaseBindingMatch</c>, con clave; FR-230): el oficial de cumplimiento
/// declara falso positivo la coincidencia vinculante de una operación bloqueada, con motivo y soporte (adjunto <c>ReceiptsScreeningReleaseSupport</c> de la operación). La
/// operación vuelve a preparación y cada persona que coincidió queda liberada para esa operación. El cajero de la sesión no puede. Auditado. (nuevo)
/// </summary>
public sealed record ReleaseBindingMatchCommand(Guid OperationPublicId, Guid SupportAttachmentPublicId, string Reason) : IRequest<Result<CashOperationDto>>,
    IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReleaseBindingMatchCommandValidator : AbstractValidator<ReleaseBindingMatchCommand>
{
    public ReleaseBindingMatchCommandValidator()
    {
        RuleFor(x => x.OperationPublicId).NotEmpty();
        RuleFor(x => x.SupportAttachmentPublicId).NotEmpty().WithMessage("Adjunte el soporte de la liberación.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("Diga por qué es un falso positivo (hasta 500 caracteres).");
    }
}

public sealed class ReleaseBindingMatchCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    SegregacionDeRecaudo segregacion,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<ReleaseBindingMatchCommand, Result<CashOperationDto>>
{
    public async Task<Result<CashOperationDto>> Handle(ReleaseBindingMatchCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        if (await OperacionesDeCaja.VisibleAsync(db, alcance, request.OperationPublicId, seguir: true, ct) is not { } v)
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
        var (operacion, sesion) = v;
        if (operacion.Status != CashOperationState.Blocked) return Result.Failure<CashOperationDto>(LiberacionesDeLista.NoBloqueada());
        if (await segregacion.MismaPersonaAsync(sesion.CashierUserId, usuario, ct)) return Result.Failure<CashOperationDto>(LiberacionesDeLista.PorElCajero());
        var soporte = await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == request.SupportAttachmentPublicId
                                                                       && a.OwnerEntityType == AdjuntosDeModulo.SoporteDeLiberacionDeLista
                                                                       && a.OwnerEntityPublicId == operacion.PublicId && a.Status == EstadoDeAdjunto.Available, ct);
        if (!soporte) return Result.Failure<CashOperationDto>(LiberacionesDeLista.SoporteInvalido());

        var coincidencias = await LiberacionesDeLista.CoincidenciasAsync(db, operacion.PublicId, ct);
        if (coincidencias.Count == 0)
            coincidencias = new[] { operacion.PayerPersonId, operacion.BeneficiaryPersonId ?? 0, operacion.DeliveredByPersonId ?? 0 }.Where(x => x != 0).Distinct().ToList();
        var ya = await LiberacionesDeLista.PersonasLiberadasAsync(db, operacion.Id, ct);
        var ahora = reloj.UtcNow;
        var motivo = request.Reason.Trim();
        foreach (var persona in coincidencias.Where(p => !ya.Contains(p)).OrderBy(p => p))
            db.ScreeningReleases.Add(new ScreeningRelease
            {
                CashOperationId = operacion.Id, PersonId = persona, Reason = motivo, SupportAttachmentPublicId = request.SupportAttachmentPublicId,
                ReleasedByUserId = usuario, ReleasedAt = ahora,
            });
        operacion.Status = CashOperationState.Preparing;
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => coincidencias.Contains(p.Id)).Select(p => p.PublicId).ToListAsync(ct);
        await auditoria.AgregarAsync("Receipts.Compliance.BindingMatchReleased", "CashOperation", operacion.PublicId, new { status = CashOperationState.Blocked.ToString() },
            new { status = CashOperationState.Preparing.ToString(), persons = publicos, reason = motivo, support = request.SupportAttachmentPublicId, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OperacionesDeCaja.DtoAsync(db, operacion, ct));
    }
}
