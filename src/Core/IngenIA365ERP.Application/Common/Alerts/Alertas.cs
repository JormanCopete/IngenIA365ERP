using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Enums.Alerts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Alerts;

/// <summary>
/// <see cref="IAlertas"/> sobre <c>COR_AlertTypes</c>, <c>COR_Alerts</c> y <c>COR_Notifications</c> (feature 012, T39,
/// T093). La entrega es por <see cref="SendNotificationCommand"/>, una por destinatario, con
/// <see cref="NotificationType.Alert"/> y el <c>AlertPublicId</c>; el correo lo manda <c>NotificationEmailDispatcher</c>
/// cuando el tipo tiene el canal. Una notificación que no se pudo registrar no tumba la alerta: queda en el registro con
/// <c>[Alertas.EntregaFallida]</c> y la alerta sigue en la bandeja de quien tiene el permiso. (nuevo)
/// </summary>
public sealed class Alertas(
    IApplicationDbContext db,
    IDestinatariosPorPermiso destinatarios,
    IActorActual actorActual,
    IDateTimeService reloj,
    ISender sender,
    ILogger<Alertas> logger) : IAlertas
{
    /// <summary>La condición canónica: <c>{TypeCode}:{entidad}[:{bodega}]</c> (data-model §22).</summary>
    public static string ClaveDe(string typeCode, Guid? entidad, Guid? bodega) =>
        $"{typeCode}:{(entidad is { } e ? e.ToString("D") : "-")}{(bodega is { } b ? ":" + b.ToString("D") : string.Empty)}";

    public async Task<Result<AlertaLevantada>> LevantarAsync(AlertaALevantar alerta, CancellationToken ct)
    {
        var definicion = TiposDeAlerta.Buscar(alerta.TypeCode);
        if (definicion is null) return Result.Failure<AlertaLevantada>(ErroresDeAlertas.TipoInexistente(alerta.TypeCode));

        var hoy = reloj.HoyLocal;
        var tipo = await db.AlertTypes
            .Where(t => t.TypeCode == alerta.TypeCode && t.ValidFrom <= hoy && (t.ValidTo == null || t.ValidTo >= hoy))
            .OrderByDescending(t => t.ValidFrom)
            .FirstOrDefaultAsync(ct);
        if (tipo is null || !tipo.IsEnabled)
        {
            logger.LogInformation("[Alertas.TipoInactivo] {TypeCode} no tiene versión vigente habilitada el {Hoy}; no se levanta.",
                alerta.TypeCode, hoy);
            return Result.Success(new AlertaLevantada(null, DesenlaceDeAlerta.TipoInactivo, 0, false));
        }

        var ahora = reloj.UtcNow;
        var clave = alerta.DedupKey ?? ClaveDe(alerta.TypeCode, alerta.EntityPublicId, alerta.ScopeWarehousePublicId);
        var pendiente = await db.Alerts.FirstOrDefaultAsync(a => a.DedupKey == clave && a.Status == AlertStatus.Pending, ct);
        if (pendiente is not null)
        {
            pendiente.Repetir(ahora);
            await db.SaveChangesAsync(ct);
            return Result.Success(new AlertaLevantada(pendiente.PublicId, DesenlaceDeAlerta.Repetida, pendiente.RecipientCount, pendiente.WithoutRecipient));
        }

        var permisos = alerta.RecipientPermissions ?? tipo.Permisos();
        var aQuien = await destinatarios.ResolverAsync(permisos, alerta.ScopeWarehousePublicId, alerta.ScopePointOfSalePublicId, ct);
        var actor = await actorActual.ObtenerAsync(ct);

        var nueva = new Alert
        {
            TypeCode = tipo.TypeCode,
            AlertTypeId = tipo.Id,
            Module = tipo.Module,
            Severity = tipo.Severity,
            Subject = Recortar(alerta.Subject, 200),
            Body = Recortar(alerta.Body, 2000),
            EntityType = alerta.EntityType,
            EntityPublicId = alerta.EntityPublicId,
            ScopeWarehousePublicId = alerta.ScopeWarehousePublicId,
            ScopePointOfSalePublicId = alerta.ScopePointOfSalePublicId,
            DedupKey = clave,
            Status = AlertStatus.Pending,
            RaisedAt = ahora,
            RaisedByKind = actor.Kind,
            RaisedByName = Recortar(actor.Name, 150),
            OccurrenceCount = 1,
            LastOccurredAt = ahora,
            RecipientCount = aQuien.Usuarios.Count,
            WithoutRecipient = aQuien.SinDestinatario,
        };
        db.Alerts.Add(nueva);
        await db.SaveChangesAsync(ct);

        if (aQuien.SinDestinatario)
        {
            logger.LogWarning("[Alertas.SinDestinatario] {TypeCode} ({DedupKey}) no tiene destinatario activo con {Permisos}; va a {Cuantos} titular(es) de CompanyAdmin.",
                tipo.TypeCode, clave, string.Join(", ", permisos), aQuien.Usuarios.Count);
        }

        var canales = NotificationChannels.InApp | (tipo.Channels.HasFlag(AlertChannels.Email) ? NotificationChannels.Email : NotificationChannels.None);
        foreach (var destinatario in aQuien.Usuarios)
        {
            var entregada = await sender.Send(new SendNotificationCommand(new NotificationPayload(
                destinatario.UserPublicId, NotificationType.Alert, nueva.Subject, nueva.Body, canales, nueva.PublicId)), ct);
            if (entregada.IsFailure)
            {
                logger.LogWarning("[Alertas.EntregaFallida] La alerta {Alerta} no se pudo notificar a {Usuario}: {Codigo}.",
                    nueva.PublicId, destinatario.UserPublicId, entregada.Error.Code);
            }
        }

        return Result.Success(new AlertaLevantada(nueva.PublicId, DesenlaceDeAlerta.Levantada, nueva.RecipientCount, nueva.WithoutRecipient));
    }

    public async Task<bool> AtenderPorProcesoAsync(string dedupKey, string nota, CancellationToken ct)
    {
        var pendiente = await db.Alerts.FirstOrDefaultAsync(a => a.DedupKey == dedupKey && a.Status == AlertStatus.Pending, ct);
        if (pendiente is null) return false;

        var actor = await actorActual.ObtenerAsync(ct);
        pendiente.Atender(actor.Kind, actor.UserId, Recortar(actor.Name, 150), Recortar(nota, 1000), reloj.UtcNow);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo] : texto;
}
