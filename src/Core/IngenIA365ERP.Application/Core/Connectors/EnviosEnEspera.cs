using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Connectors;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Connectors;

/// <summary>
/// Los envíos que esperan a un conector (feature 020, R18; T164): deshabilitado o sin credenciales, el consumidor deja el envío
/// <c>OnHold</c> y su entrega reintenta con espera creciente. Al habilitar de nuevo o al poner credenciales, sus entregas se adelantan a
/// ahora para que salgan en la siguiente pasada. No guarda: lo hace quien llama.
/// </summary>
public static class EnviosEnEspera
{
    public static async Task DespertarAsync(IApplicationDbContext db, int conectorId, DateTime ahora, CancellationToken ct)
    {
        var mensajes = await db.ConnectorRequests.Where(r => r.ConnectorId == conectorId && r.Status == ConnectorRequestStatus.OnHold)
            .Select(r => r.MessagePublicId).ToListAsync(ct);
        if (mensajes.Count == 0) return;
        var entregas = await db.IntegrationMessageDeliveries
            .Where(d => d.Destination == IntegrationDestinations.Connectors && d.Status == DeliveryStatus.Pending && mensajes.Contains(d.Message!.PublicId))
            .ToListAsync(ct);
        foreach (var e in entregas) e.NextAttemptAt = ahora;
    }
}
