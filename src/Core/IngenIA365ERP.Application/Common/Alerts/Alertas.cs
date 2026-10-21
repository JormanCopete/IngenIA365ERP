using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
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
    ILogger<Alertas> logger,
    IFiltroDeAlcanceDeAlertas? filtroDeAlcance = null) : IAlertas
{
    /// <summary>La condición canónica: <c>{TypeCode}:{entidad}[:{bodega}]</c> (data-model §22).</summary>
    public static string ClaveDe(string typeCode, Guid? entidad, Guid? bodega) =>
        $"{typeCode}:{(entidad is { } e ? e.ToString("D") : "-")}{(bodega is { } b ? ":" + b.ToString("D") : string.Empty)}";

    /// <summary>El largo de <c>COR_Alerts.DedupKey</c>.</summary>
    public const int LargoDeLaClave = 200;

    /// <summary>
    /// La condición tal como se guarda: hasta <see cref="LargoDeLaClave"/> caracteres sin cambio; más larga —una combinación de dimensiones de
    /// Cartera puede serlo—, su comienzo y la huella SHA-256 de la clave entera, así dos claves distintas no se confunden y la misma siempre da
    /// lo mismo. Hasta el 2026-10-13 una clave larga reventaba el guardado (<c>varchar(200)</c>) y con él la operación que alertaba.
    /// </summary>
    public static string ClaveAcotada(string clave)
    {
        if (clave.Length <= LargoDeLaClave) return clave;
        var huella = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(clave)));
        return $"{clave[..(LargoDeLaClave - huella.Length - 1)]}#{huella}";
    }

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
        var clave = ClaveAcotada(alerta.DedupKey ?? ClaveDe(alerta.TypeCode, alerta.EntityPublicId, alerta.ScopeWarehousePublicId));
        var pendiente = await db.Alerts.FirstOrDefaultAsync(a => a.DedupKey == clave && a.Status == AlertStatus.Pending, ct);
        if (pendiente is not null)
        {
            await RepetirAsync(pendiente, ahora, ct);
            return Result.Success(new AlertaLevantada(pendiente.PublicId, DesenlaceDeAlerta.Repetida, pendiente.RecipientCount, pendiente.WithoutRecipient));
        }

        // Feature 013 (PM-15, C68): de la lista del tipo, el módulo de origen descarta los permisos del otro módulo; una lista
        // explícita (el nivel pendiente, la bandeja de integración) se respeta tal cual.
        var permisos = alerta.RecipientPermissions ?? FiltroPorModuloDeOrigen.Aplicar(tipo.Permisos(), alerta.ModuloDeOrigen);
        var aQuien = await destinatarios.ResolverAsync(permisos, alerta.ScopeWarehousePublicId, alerta.ScopePointOfSalePublicId, ct);
        aQuien = await AlcanceDeCarteraAsync(aQuien, alerta, ct);
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
            ScopeBranchPublicId = alerta.ScopeBranchPublicId,
            ScopeCreditLinePublicId = alerta.ScopeCreditLinePublicId,
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
        // Dos operaciones a la vez pueden levantar la misma condición (dos recaudadores contra la misma regla faltante): la segunda choca con
        // UK_COR_Alerts_DedupKey_Pending. El guardado va bajo un punto de guardado para que ese choque no aborte la transacción de quien alerta
        // (en PostgreSQL la dejaría inutilizable) y se resuelve como repetición de la que ganó. Hasta el 2026-10-13 tumbaba el pago.
        var fallo = await PuntoDeGuardado.EjecutarAsync<Exception?>(db, "alerta", async () =>
        {
            db.Alerts.Add(nueva);
            await db.SaveChangesAsync(ct);
            return null;
        }, e => e, ct);
        if (fallo is not null)
        {
            var ganadora = await db.Alerts.FirstOrDefaultAsync(a => a.DedupKey == clave && a.Status == AlertStatus.Pending, ct);
            if (ganadora is null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(fallo).Throw();
            await RepetirAsync(ganadora!, ahora, ct);
            return Result.Success(new AlertaLevantada(ganadora.PublicId, DesenlaceDeAlerta.Repetida, ganadora.RecipientCount, ganadora.WithoutRecipient));
        }

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

    /// <summary>
    /// Cuenta una repetición de la alerta pendiente. Es un contador de cortesía: si otra operación la repitió a la vez (su <c>RowVersion</c>
    /// cambió), esta repetición se pierde en lugar de tumbar la operación que alertaba —bajo un punto de guardado, para no abortar la transacción
    /// de quien llama—. Hasta el 2026-10-13 dos pagos simultáneos contra la misma regla faltante devolvían 409 al recaudador.
    /// </summary>
    private async Task RepetirAsync(Alert alerta, DateTime ahora, CancellationToken ct)
    {
        var fallo = await PuntoDeGuardado.EjecutarAsync<Exception?>(db, "alerta-repetida", async () =>
        {
            alerta.Repetir(ahora);
            await db.SaveChangesAsync(ct);
            return null;
        }, e => e, ct);
        if (fallo is null) return;
        if (fallo is not (Domain.Exceptions.ConcurrencyConflictException or DbUpdateConcurrencyException))
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(fallo).Throw();
        if (db is DbContext contexto) contexto.Entry(alerta).State = EntityState.Detached;
        logger.LogInformation("[Alertas.RepeticionConcurrente] {TypeCode} ({DedupKey}): otra operación la repitió a la vez; esta repetición no se cuenta.",
            alerta.TypeCode, alerta.DedupKey);
    }

    /// <summary>
    /// Feature 013 (C68, SC-015): con oficina o línea, sólo quienes las tienen en su alcance (<see cref="IFiltroDeAlcanceDeAlertas"/>).
    /// Si el filtro no deja a nadie, los titulares de <c>CompanyAdmin</c> con <c>WithoutRecipient</c> (SC-022).
    /// </summary>
    private async Task<DestinatariosDeAlerta> AlcanceDeCarteraAsync(DestinatariosDeAlerta aQuien, AlertaALevantar alerta, CancellationToken ct)
    {
        if (filtroDeAlcance is null || aQuien.SinDestinatario || (alerta.ScopeBranchPublicId is null && alerta.ScopeCreditLinePublicId is null))
            return aQuien;
        var conAlcance = await filtroDeAlcance.FiltrarAsync(aQuien.Usuarios, alerta.ScopeBranchPublicId, alerta.ScopeCreditLinePublicId, ct);
        if (conAlcance.Count > 0) return new DestinatariosDeAlerta(conAlcance, SinDestinatario: false);
        return await destinatarios.ResolverAsync([], null, null, ct) is { } respaldo
            ? respaldo with { SinDestinatario = true }
            : new DestinatariosDeAlerta([], SinDestinatario: true);
    }

    public async Task<bool> AtenderPorProcesoAsync(string dedupKey, string nota, CancellationToken ct)
    {
        dedupKey = ClaveAcotada(dedupKey);
        var pendiente = await db.Alerts.FirstOrDefaultAsync(a => a.DedupKey == dedupKey && a.Status == AlertStatus.Pending, ct);
        if (pendiente is null) return false;

        var actor = await actorActual.ObtenerAsync(ct);
        pendiente.Atender(actor.Kind, actor.UserId, Recortar(actor.Name, 150), Recortar(nota, 1000), reloj.UtcNow);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo] : texto;
}
