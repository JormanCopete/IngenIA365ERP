using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Common;

/// <summary>Lo que un documento de Tesorería pide aprobar (lote, uso del fondo de liquidez). (nuevo)</summary>
public sealed record PedidoDeAprobacionDiferida(
    string Sujeto,
    Guid FuentePublicId,
    string Rotulo,
    decimal Monto,
    DateOnly Fecha,
    string Huella,
    string? PermisoLimitado,
    IReadOnlyCollection<int> Participantes);

/// <summary>El estado de la aprobación de un documento: si la exige, en qué va y quiénes aprobaron (<c>SEC_Users.Id</c>). (nuevo)</summary>
public sealed record EstadoDeAprobacionDiferida(Guid? ApprovalRequestPublicId, ApprovalRequestStatus? Status, int NivelActual, IReadOnlyList<int> Aprobadores)
{
    public static EstadoDeAprobacionDiferida SinSolicitud { get; } = new(null, null, 0, []);

    public bool Aprobada => Status == ApprovalRequestStatus.Approved;
}

/// <summary>
/// La aprobación <b>diferida</b> multinivel de la Tesorería nueva (feature 015, T0061; US12; R-31; FR-013, FR-110, FR-207; US12-3), sobre el motor de la 012:
/// el documento pide su aprobación y queda esperando; los niveles los da la política de la cooperativa del sujeto (<c>PaymentBatchApproval</c>,
/// <c>LiquidityFundUse</c>) por umbrales sucesivos —el que tiene una atribución de 50 millones cuenta como un nivel y un lote de 80 espera a quien complete
/// el monto— o, sin política, la regla fija de un nivel con el permiso con monto, cuyo aprobador tiene que tener una atribución que alcance
/// (<see cref="FuenteDeAprobacionDeTesoreria"/>). <b>Aprobar no mueve dinero</b>: la última aprobación sólo deja el documento aprobado por su fuente. La
/// solicitud se guarda con el documento (en su misma unidad de trabajo): a diferencia de la caja, el documento no se revierte al pedirla. (nuevo)
/// </summary>
public sealed class AprobacionDiferidaEnTesoreria(IApplicationDbContext db, IMotorDeAprobaciones motor, IActorActual actorActual)
{
    /// <summary>Pide la aprobación (sin guardar). <c>Success(null)</c> si el monto no la exige.</summary>
    public async Task<Result<ApprovalRequest?>> PedirAsync(PedidoDeAprobacionDiferida pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } solicitante)
            throw new InvalidOperationException("La aprobación de Tesorería exige una persona resuelta en SEC_Users (IActorActual.UserId).");
        var rotulo = pedido.Rotulo.Length <= 200 ? pedido.Rotulo : pedido.Rotulo[..200];
        return await motor.SolicitarAsync(new SolicitudDeAprobacion(pedido.Sujeto, ApprovalSourceTypes.TreasuryDocument, pedido.FuentePublicId, rotulo,
            null, null, null, pedido.Monto, pedido.Fecha, solicitante, pedido.Participantes, pedido.Huella, pedido.PermisoLimitado), ct);
    }

    /// <summary>El estado de la última solicitud del documento y sujeto.</summary>
    public async Task<EstadoDeAprobacionDiferida> EstadoAsync(Guid fuente, string sujeto, CancellationToken ct)
    {
        var solicitud = await db.ApprovalRequests.AsNoTracking().Include(r => r.Decisions)
            .Where(r => r.SourceType == ApprovalSourceTypes.TreasuryDocument && r.SourcePublicId == fuente && r.Subject == sujeto)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (solicitud is null) return EstadoDeAprobacionDiferida.SinSolicitud;
        var aprobadores = solicitud.Decisions.Where(d => d.Decision == ApprovalDecisionKind.Approve).OrderBy(d => d.Level).Select(d => d.DecidedByUserId).ToList();
        return new EstadoDeAprobacionDiferida(solicitud.PublicId, solicitud.Status, solicitud.CurrentLevel, aprobadores);
    }
}
