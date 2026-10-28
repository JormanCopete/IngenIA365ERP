using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Documento;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Identity.Correccion.FusionarIdentidades;

/// <summary>
/// Feature 017 (US6, T069, FR-035): el maestro junta dos identidades que resultaron ser la misma persona
/// (dos correos, un documento). Dos pasos, con un caso <c>Merge</c> que los sigue:
/// <list type="number">
///   <item>el administrativo, en una transacción (<see cref="IFusionDeIdentidades"/>): membresías,
///         credenciales, invitaciones e historial pasan a <c>Target</c>; <c>Source</c> queda deshabilitada y
///         con el sello rotado. El caso queda <c>InProgress</c>;</item>
///   <item>el de cada cooperativa: <c>SEC_Users.CentralUserId</c> pasa al destino. Si alguna base falla, el
///         caso sigue <c>InProgress</c> con las pendientes en <c>Detail</c>, y repetir el mismo pedido
///         completa sólo lo que falta. Sin pendientes, el caso se cierra <c>Done</c>.</item>
/// </list>
/// Se niega con <c>Identity.Merge.SharedTenant</c> si las dos tienen fila en una misma cooperativa: allí habría
/// dos usuarios para una persona y elegir cuál sobrevive es una decisión de esa cooperativa, no de soporte.
/// </summary>
public sealed record FusionarIdentidadesCommand(Guid SourceId, Guid TargetId, string? Reason)
    : IRequest<Result<FusionResult>>;

public sealed record FusionResult(Guid CasePublicId, string Status, IReadOnlyList<Guid> PendingTenants);

public sealed class FusionarIdentidadesCommandValidator : AbstractValidator<FusionarIdentidadesCommand>
{
    public FusionarIdentidadesCommandValidator()
    {
        RuleFor(x => x.SourceId).NotEmpty();
        RuleFor(x => x.TargetId).NotEmpty().NotEqual(x => x.SourceId).WithMessage("Elija dos identidades distintas.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo es obligatorio.").MaximumLength(500);
    }
}

public sealed class FusionarIdentidadesCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext db,
    IFusionDeIdentidades fusion,
    PropagadorDeIdentidad propagador,
    AuditoriaDeIdentidad auditoria,
    IDateTimeService clock)
    : IRequestHandler<FusionarIdentidadesCommand, Result<FusionResult>>
{
    public async Task<Result<FusionResult>> Handle(FusionarIdentidadesCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.Purpose != CentralJwtPurposes.Full || !currentUser.IsGlobalMasterAdmin
            || currentUser.CentralUserId is not { } actor)
            return Result.Failure<FusionResult>("Saas.MasterOnly", "Sólo el maestro de plataforma.");

        var origen = await centralIdentity.FindByIdAsync(request.SourceId, ct);
        var destino = await centralIdentity.FindByIdAsync(request.TargetId, ct);
        if (origen is null || destino is null)
            return Result.Failure<FusionResult>("Identity.NotFound", "Identidad no encontrada.");
        if (origen.IsGlobalMasterAdmin)
            return Result.Failure<FusionResult>("Identity.Merge.MasterSource", "La cuenta del maestro no se fusiona en otra.");
        if (destino.Status == CentralUserStatus.Disabled)
            return Result.Failure<FusionResult>("Identity.Merge.TargetDisabled", "La identidad destino está deshabilitada.");

        // Un reintento llega con el caso de la primera vez abierto: se reusa, y el paso administrativo ya
        // no mueve nada. Sin caso abierto, el origen todavía no puede estar deshabilitado.
        var caso = await db.IdentityCases
            .Where(c => c.Kind == IdentityCaseKind.Merge && c.CentralUserId == origen.Id && c.OtherCentralUserId == destino.Id
                        && (c.Status == IdentityCaseStatus.Open || c.Status == IdentityCaseStatus.InProgress))
            .FirstOrDefaultAsync(ct);
        if (caso is null)
        {
            if (origen.Status == CentralUserStatus.Disabled)
                return Result.Failure<FusionResult>("Identity.Merge.SourceDisabled", "La identidad origen ya está deshabilitada.");

            var deOrigen = await db.TenantMemberships.AsNoTracking()
                .Where(m => m.CentralUserId == origen.Id).Select(m => m.TenantId).ToListAsync(ct);
            var compartida = await db.TenantMemberships.AsNoTracking()
                .AnyAsync(m => m.CentralUserId == destino.Id && deOrigen.Contains(m.TenantId), ct);
            if (compartida)
                return Result.Failure<FusionResult>(
                    "Identity.Merge.SharedTenant",
                    "Las dos identidades están en una misma cooperativa. Esa cooperativa debe retirar a una antes de fusionarlas.");

            caso = IdentityCase.Abrir(IdentityCaseKind.Merge, origen.Id, destino.Id, request.Reason, actor);
            db.IdentityCases.Add(caso);
        }

        var now = clock.UtcNow;
        var movido = await fusion.TrasladarAsync(origen.Id, destino.Id, now, ct);
        await centralIdentity.RecalcularSegundoFactorAsync(destino.Id, ct);
        await centralIdentity.RecalcularSegundoFactorAsync(origen.Id, ct);
        var usuarioDelDestino = movido.DocumentUsernameDelDestino;
        if (movido.DocumentoTipoParaElDestino is not null
            && Domain.Common.Identidad.UsuarioDeDocumento.Validar(
                movido.DocumentoTipoParaElDestino, movido.DocumentoNumeroParaElDestino, soloDeAcceso: false, out _) is { } doc
            && (await centralIdentity.SetDocumentAsync(destino.Id, doc, ct)).Succeeded)
        {
            db.DocumentHistory.Add(DocumentHistoryEntry.Registrar(
                destino.Id, null, null, null, doc.Tipo.Codigo, doc.Numero, doc.Usuario,
                DocumentChangeReason.Merge, DocumentChangeActor.PlatformMaster, actor, now, notes: request.Reason));
            usuarioDelDestino = doc.Usuario;
        }
        caso.EnCurso($"Paso administrativo hecho: {movido.Membresias} membresías, {movido.Credenciales} credenciales, " +
                     $"{movido.Invitaciones} invitaciones, {movido.Historial} cambios de documento.");
        await db.SaveChangesAsync(ct);

        var pendientes = await propagador.VinculoAsync(origen.Id, destino.Id, usuarioDelDestino, ct);
        if (pendientes.Count == 0)
            caso.Cerrar(IdentityCaseStatus.Done, actor, request.Reason, now);
        else
            caso.EnCurso($"Pendiente en las cooperativas: {string.Join(", ", pendientes)}. Repita la fusión para completarla.");
        await db.SaveChangesAsync(ct);

        await auditoria.GlobalAsync(
            AuditEventTypes.IdentityMerged, actor, currentUser.Email, nameof(CentralUser), destino.Id,
            new { Origen = origen.Id, origen.Email }, new { Destino = destino.Id, destino.Email, request.Reason, Pendientes = pendientes },
            now, ct);

        return Result.Success(new FusionResult(caso.PublicId, caso.Status.ToString(), pendientes));
    }
}
