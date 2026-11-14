using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Channel.Support;

// Feature 021, US12 (T121; FR-058, FR-061): el funcionario desbloquea la cuenta del canal de un asociado (quita el freno de ingresos por fallos) o
// cierra todas sus sesiones. Lo hace el canal; el canal comprueba que la cuenta sea de una persona habilitada en esta cooperativa (FR-059). Cada acción
// queda auditada en el ERP y en el canal.

/// <summary><c>POST /api/channel/support/accounts/{id}/unlock</c> (<c>Channel.Support.Manage</c>).</summary>
public sealed record UnlockChannelAccountCommand(Guid AccountId) : IRequest<Result>;

/// <summary><c>POST /api/channel/support/accounts/{id}/revoke-sessions</c> (<c>Channel.Support.Manage</c>).</summary>
public sealed record RevokeChannelSessionsCommand(Guid AccountId) : IRequest<Result>;

public sealed class UnlockChannelAccountCommandHandler(IApplicationDbContext db, ICanalSatelite canal, ICurrentTenantService cooperativa, AuditoriaDelCanal auditoria)
    : IRequestHandler<UnlockChannelAccountCommand, Result>
{
    public const string Evento = "Channel.Support.AccountUnlocked";

    public Task<Result> Handle(UnlockChannelAccountCommand request, CancellationToken ct) =>
        AccionesDeAtencion.EjecutarAsync(db, canal, cooperativa, auditoria, Evento, request.AccountId,
            (coop, c) => canal.DesbloquearCuentaAsync(coop, request.AccountId, c), ct);
}

public sealed class RevokeChannelSessionsCommandHandler(IApplicationDbContext db, ICanalSatelite canal, ICurrentTenantService cooperativa, AuditoriaDelCanal auditoria)
    : IRequestHandler<RevokeChannelSessionsCommand, Result>
{
    public const string Evento = "Channel.Support.SessionsRevoked";

    public Task<Result> Handle(RevokeChannelSessionsCommand request, CancellationToken ct) =>
        AccionesDeAtencion.EjecutarAsync(db, canal, cooperativa, auditoria, Evento, request.AccountId,
            (coop, c) => canal.CerrarSesionesAsync(coop, request.AccountId, c), ct);
}

internal static class AccionesDeAtencion
{
    public static async Task<Result> EjecutarAsync(IApplicationDbContext db, ICanalSatelite canal, ICurrentTenantService cooperativa, AuditoriaDelCanal auditoria,
        string evento, Guid cuenta, Func<Guid, CancellationToken, Task<Result>> accion, CancellationToken ct)
    {
        if (!canal.Configurado || !Guid.TryParse(cooperativa.TenantId, out var coop))
            return Result.Failure("Channel.Satellite.NotConfigured", "El canal digital no está configurado en este ambiente.");
        var r = await accion(coop, ct);
        if (r.IsFailure) return r;
        await auditoria.AgregarAsync(evento, "ChannelAccount", cuenta, null, new { account = cuenta }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
