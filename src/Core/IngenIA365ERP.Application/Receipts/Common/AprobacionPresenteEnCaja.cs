using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>Lo que una operación de caja pide aprobar con el aprobador presente. (nuevo)</summary>
/// <param name="Sujeto">Uno de <see cref="ApprovalSubjects"/> de Recaudo (<c>CashCountDifference</c>, <c>CashMeansReclassification</c>…).</param>
/// <param name="FuentePublicId">El documento que se aprueba (la sesión, la operación de caja, el recibo).</param>
/// <param name="Rotulo">Lo que la bandeja muestra.</param>
/// <param name="Monto">El valor de lo que se aprueba (la diferencia, lo reclasificado, el recibo).</param>
/// <param name="Fecha">La fecha de la operación (Colombia).</param>
/// <param name="Huella">La huella de lo que se aprueba: si cambia, la aprobación ya no vale.</param>
/// <param name="PermisoLimitado">El permiso con monto (<c>Receipts.Count.ApproveDifference</c>…), o nulo.</param>
/// <param name="Participantes"><c>SEC_Users.Id</c> que no pueden aprobar (además de quien pide): el cajero de la sesión, quien registró.</param>
/// <param name="AprobacionPublicId">La solicitud que trae el encabezado <c>X-Approval-Request</c> al repetir la operación.</param>
public sealed record PedidoDeAprobacionPresente(
    string Sujeto,
    Guid FuentePublicId,
    string Rotulo,
    decimal Monto,
    DateOnly Fecha,
    string Huella,
    string? PermisoLimitado,
    IReadOnlyCollection<int> Participantes,
    Guid? AprobacionPublicId);

/// <summary>La aprobación con que sigue la operación: la solicitud aprobada y quien aprobó, o nada si no hizo falta. (nuevo)</summary>
public sealed record AprobacionPresenteObtenida(Guid? ApprovalRequestPublicId, int? AprobadorUserId)
{
    public static AprobacionPresenteObtenida NoHizoFalta { get; } = new(null, null);
}

/// <summary>
/// La aprobación con el aprobador <b>presente</b> de la caja (feature 015, T0061; US12; R-31; FR-013, FR-032, FR-192; contracts/api.md §1.4; DT §2.7), sobre
/// el motor de la 012 (<see cref="IMotorDeAprobaciones"/>, <c>AprobadorPresente</c>, <c>IDesafiosDePresencia</c>):
/// <list type="number">
/// <item>la primera vez, el motor evalúa el sujeto (la política de la cooperativa o la regla fija de un nivel) y, si hace falta, crea la solicitud
/// sellada con quien pide y los <see cref="PedidoDeAprobacionPresente.Participantes"/> excluidos; la operación responde <c>428</c>
/// <c>Approvals.PresenceRequired</c> con <c>approvalRequestPublicId</c> y el monto. La solicitud se guarda en un contexto aparte
/// (<see cref="ITenantDbContextFactory.AbrirLaDelAmbito"/>), porque la transacción de la operación se revierte con el 428;</item>
/// <item>el aprobador se identifica en el equipo del cajero con su llave o su código (<c>…/approvals/{id}/presence-challenge</c> y <c>…/decide</c>; las
/// fallas del desafío son <c>422</c> con los <c>Approvals.Presence.*</c> de la 012, nunca <c>401</c>);</item>
/// <item>la operación se repite con la <b>misma</b> <c>Idempotency-Key</c> y el encabezado <c>X-Approval-Request</c>: si la solicitud es de esta fuente,
/// sujeto y huella y está <c>Approved</c>, sigue con su aprobador; pendiente, vuelve a responder 428; rechazada o retirada, <c>Receipts.Approval.Rejected</c>.</item>
/// </list>
/// Aprobar no mueve dinero: la operación lo hace al repetirse. Quien aprueba es otra persona (el motor excluye a quien pide y a los participantes, y la
/// fuente compara personas). (nuevo)
/// </summary>
public sealed class AprobacionPresenteEnCaja(IApplicationDbContext db, IMotorDeAprobaciones motor, IActorActual actorActual, IServiceProvider servicios)
{
    public async Task<Result<AprobacionPresenteObtenida>> ExigirAsync(PedidoDeAprobacionPresente pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } solicitante)
            throw new InvalidOperationException("La aprobación presente exige una persona resuelta en SEC_Users (IActorActual.UserId).");

        if (pedido.AprobacionPublicId is { } pedida)
        {
            var traida = await db.ApprovalRequests.AsNoTracking().Include(r => r.Decisions)
                .FirstOrDefaultAsync(r => r.PublicId == pedida && r.SourceType == ApprovalSourceTypes.ReceiptsOperation, ct);
            if (traida is null || traida.Subject != pedido.Sujeto || traida.SourcePublicId != pedido.FuentePublicId
                || !string.Equals(traida.ContentSha256, pedido.Huella, StringComparison.OrdinalIgnoreCase))
                return Result.Failure<AprobacionPresenteObtenida>(ErroresDeRecaudo.AprobacionNoCorresponde(pedida));
            return Desenlace(traida, pedido);
        }

        // Una solicitud viva de la misma fuente, sujeto y huella se reusa (un reintento sin el encabezado no pide otra).
        var previa = await db.ApprovalRequests.AsNoTracking().Include(r => r.Decisions)
            .Where(r => r.SourceType == ApprovalSourceTypes.ReceiptsOperation && r.SourcePublicId == pedido.FuentePublicId && r.Subject == pedido.Sujeto
                        && r.ContentSha256 == pedido.Huella
                        && (r.Status == ApprovalRequestStatus.Pending || r.Status == ApprovalRequestStatus.Approved))
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (previa is not null) return Desenlace(previa, pedido);

        var rotulo = pedido.Rotulo.Length <= 200 ? pedido.Rotulo : pedido.Rotulo[..200];
        var solicitada = await motor.SolicitarAsync(new SolicitudDeAprobacion(pedido.Sujeto, ApprovalSourceTypes.ReceiptsOperation, pedido.FuentePublicId,
            rotulo, null, null, null, pedido.Monto, pedido.Fecha, solicitante, pedido.Participantes, pedido.Huella, pedido.PermisoLimitado), ct);
        if (solicitada.IsFailure) return Result.Failure<AprobacionPresenteObtenida>(solicitada.Error);
        if (solicitada.Value is not { } nueva) return Result.Success(AprobacionPresenteObtenida.NoHizoFalta);

        await GuardarFueraDeLaTransaccionAsync(nueva, ct);
        return Result.Failure<AprobacionPresenteObtenida>(ErroresDeRecaudo.PresenciaRequerida(nueva.PublicId, pedido.Sujeto, pedido.Monto,
            VistaDeSolicitudes.NivelActual(nueva)?.PermissionCode ?? string.Empty));
    }

    private static Result<AprobacionPresenteObtenida> Desenlace(ApprovalRequest solicitud, PedidoDeAprobacionPresente pedido) => solicitud.Status switch
    {
        ApprovalRequestStatus.Approved => Result.Success(new AprobacionPresenteObtenida(solicitud.PublicId,
            solicitud.Decisions.Where(d => d.Decision == ApprovalDecisionKind.Approve).OrderByDescending(d => d.Level).ThenByDescending(d => d.Id)
                .Select(d => (int?)d.DecidedByUserId).FirstOrDefault())),
        ApprovalRequestStatus.Pending => Result.Failure<AprobacionPresenteObtenida>(ErroresDeRecaudo.PresenciaRequerida(solicitud.PublicId, pedido.Sujeto,
            pedido.Monto, VistaDeSolicitudes.NivelActual(solicitud)?.PermissionCode ?? string.Empty)),
        _ => Result.Failure<AprobacionPresenteObtenida>(ErroresDeRecaudo.AprobacionRechazada(solicitud.PublicId)),
    };

    /// <summary>
    /// La solicitud tiene que sobrevivir al 428, que revierte la transacción de la operación: se saca del contexto de la operación y se guarda en uno propio
    /// de la misma cooperativa. Sin transacción en curso (las pruebas, InMemory) se guarda en el mismo.
    /// </summary>
    private async Task GuardarFueraDeLaTransaccionAsync(ApprovalRequest solicitud, CancellationToken ct)
    {
        var fabrica = TransaccionExplicita.HayEnCurso(db) ? servicios.GetService<ITenantDbContextFactory>() : null;
        if (fabrica is null || db is not DbContext propio)
        {
            await db.SaveChangesAsync(ct);
            return;
        }
        propio.Entry(solicitud).State = EntityState.Detached;
        await using var aparte = fabrica.AbrirLaDelAmbito();
        aparte.Db.ApprovalRequests.Add(solicitud);
        await aparte.Db.SaveChangesAsync(CancellationToken.None);
    }
}
