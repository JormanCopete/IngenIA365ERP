using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Storage.Services;

/// <summary>
/// T119 — Background service que despacha correo para las notificaciones
/// con <c>EmailStatus = "Pending"</c>. Toma lotes pequeños y los envía via
/// <see cref="IEmailSender"/> (que ya hace backoff interno). Si un envío
/// falla más de <see cref="MaxAttempts"/> veces, marca <c>Failed</c> y
/// persiste el <see cref="NotificationDeliveryFailure"/> para diagnóstico.
///
/// <para>
/// El correo del destinatario se resuelve mirando el <c>User</c> por
/// PublicId; si el user no tiene email, marcamos <c>Failed</c> inmediato
/// con motivo claro (no hay reintento útil).
/// </para>
///
/// <para>
/// Feature 012 (T051; decisiones-transversales T39, T47; pregunta B5): cada cooperativa se despacha por
/// <see cref="IEjecutorEnCooperativa"/> —su base, su auditoría y el actor «Proceso de integración» con
/// origen <c>Tarea:email.dispatch</c>— y sólo si esta réplica toma el arrendamiento <c>email.dispatch</c>
/// de esa cooperativa, que suelta al terminar el lote. Antes, con dos réplicas, las dos leían el mismo
/// lote pendiente y el correo salía dos veces. Lo registra sólo <c>API/Program.cs</c>, condicionado a
/// <c>Integration:EmailDispatcher:Enabled</c>, y no toca nada hasta que la base esté lista; el DbMigrator
/// nunca lo arranca.
/// </para>
///
/// <para>
/// Guarda con <c>SaveChangesAsync</c> propio, como antes: el estado del correo de una notificación es
/// técnico (enviado, reintentos, falla) y pasarlo por un comando dejaría un evento de auditoría cada
/// quince segundos por cooperativa. Si la regla «escribe sólo por comandos» de
/// <c>NingunTrabajoDeFondoOperaSinCooperativa</c> lo alcanza, va en su lista de excepciones con este motivo.
/// </para>
/// </summary>
public sealed class NotificationEmailDispatcher : BackgroundService
{
    private const int BatchSize = 25;
    private const int MaxAttempts = 4;
    private static readonly TimeSpan IntervaloPorDefecto = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan EsperaDeLaBase = TimeSpan.FromSeconds(5);
    private static readonly string Origen = Actor.OrigenDeTarea(NombresDeArrendamiento.Correo);

    private readonly IServiceScopeFactory _ambitos;
    private readonly IEjecutorEnCooperativa _ejecutor;
    private readonly ILogger<NotificationEmailDispatcher> _logger;
    private readonly Func<bool> _baseLista;
    private readonly TimeSpan _intervalo;

    /// <param name="ambitos">Para leer el directorio de cooperativas, que no es de ninguna cooperativa.</param>
    /// <param name="ejecutor">El único camino a los datos de una cooperativa.</param>
    /// <param name="logger">Registro.</param>
    /// <param name="baseLista">Si la base ya está migrada y sembrada (<c>DatabaseReadiness.IsReady</c>); sin ella, siempre.</param>
    /// <param name="intervalo">Entre pasadas (<c>Integration:EmailDispatcher:IntervalSeconds</c>); por defecto 15 s.</param>
    public NotificationEmailDispatcher(
        IServiceScopeFactory ambitos,
        IEjecutorEnCooperativa ejecutor,
        ILogger<NotificationEmailDispatcher> logger,
        Func<bool>? baseLista = null,
        TimeSpan? intervalo = null)
    {
        _ambitos = ambitos;
        _ejecutor = ejecutor;
        _logger = logger;
        _baseLista = baseLista ?? (() => true);
        _intervalo = intervalo is { } i && i > TimeSpan.Zero ? i : IntervaloPorDefecto;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "NotificationEmailDispatcher iniciado — poll cada {Seconds}s, batch {Batch}.",
            _intervalo.TotalSeconds, BatchSize);

        while (!_baseLista())
        {
            if (!await EsperarAsync(EsperaDeLaBase, stoppingToken)) return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DespacharUnaPasadaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "NotificationEmailDispatcher: error procesando batch; siguiente intento en {Seconds}s.",
                    _intervalo.TotalSeconds);
            }

            if (!await EsperarAsync(_intervalo, stoppingToken)) break;
        }
    }

    /// <summary>
    /// Un lote POR COOPERATIVA, no uno global, cada uno por <see cref="IEjecutorEnCooperativa"/> y bajo
    /// el arrendamiento <c>email.dispatch</c> de esa cooperativa. Una cooperativa que falla se registra
    /// y sigue la siguiente. Pública para que las pruebas conduzcan una pasada a mano.
    ///
    /// <para>
    /// Antes de la feature 004 sacaba un solo <c>IApplicationDbContext</c> del contenedor y barría las
    /// notificaciones pendientes sin filtrar por cooperativa; con una base por cooperativa ese contexto,
    /// creado sin petición, no encontraba nada y el correo dejaba de salir sin un solo error.
    /// </para>
    /// </summary>
    public async Task DespacharUnaPasadaAsync(CancellationToken ct)
    {
        if (!_baseLista()) return;

        foreach (var cooperativa in await CooperativasAsync(ct))
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                await _ejecutor.EjecutarAsync(cooperativa, Actor.ProcesoDeIntegracion(Origen), Origen, DespacharEnAsync, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "NotificationEmailDispatcher: falló el lote de {Cooperativa} ({TenantPublicId}); siguen las demás.",
                    cooperativa.Name, cooperativa.PublicId);
            }
        }
    }

    /// <summary>Dentro de la cooperativa: toma el arrendamiento, despacha un lote y lo suelta, falle o no.</summary>
    private static async Task DespacharEnAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var arrendamientos = servicios.GetRequiredService<IArrendamientos>();
        if (!await arrendamientos.ArrendarAsync(NombresDeArrendamiento.Correo, null, ct)) return;

        try
        {
            await ProcesarLoteDeAsync(servicios.GetRequiredService<IApplicationDbContext>(), servicios, ct);
        }
        finally
        {
            await arrendamientos.SoltarAsync(NombresDeArrendamiento.Correo, CancellationToken.None);
        }
    }

    /// <summary>
    /// Las cooperativas activas con base propia. Si el directorio falla, se registra
    /// y se devuelve vacio: el despachador reintenta al siguiente ciclo, y tumbar el
    /// servicio de fondo por un fallo transitorio de lectura seria peor.
    /// </summary>
    private async Task<IReadOnlyList<TenantDirectoryEntry>> CooperativasAsync(CancellationToken ct)
    {
        try
        {
            await using var ambito = _ambitos.CreateAsyncScope();
            var todas = await ambito.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(ct);
            return [.. todas.Where(c =>
                !string.IsNullOrWhiteSpace(c.DatabaseName) ||
                !string.IsNullOrWhiteSpace(c.ConnectionString))];
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex,
                "No se pudieron enumerar las cooperativas para despachar correo. " +
                "Se reintenta en el siguiente ciclo.");
            return [];
        }
    }

    /// <summary>Espera; falso si se pidió detener el servicio.</summary>
    private static async Task<bool> EsperarAsync(TimeSpan cuanto, CancellationToken ct)
    {
        try
        {
            await Task.Delay(cuanto, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task ProcesarLoteDeAsync(
        IApplicationDbContext db, IServiceProvider servicios, CancellationToken ct)
    {
        var sender = servicios.GetRequiredService<IEmailSender>();
        var templates = servicios.GetRequiredService<INotificationTemplateRenderer>();

        var batch = await db.Notifications
            .Where(n => n.EmailStatus == "Pending"
                     && (n.ChannelsMask & 2) == 2  // 2 = NotificationChannels.Email
                     && n.EmailAttemptCount < MaxAttempts)
            .OrderBy(n => n.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0) return;

        // Resuelve emails de los destinatarios en un solo round-trip.
        var publicIds = batch.Select(n => n.RecipientUserPublicId).Distinct().ToList();
        var recipients = await db.Users
            .Where(u => publicIds.Contains(u.PublicId))
            .Select(u => new { u.PublicId, u.Email })
            .ToListAsync(ct);
        var emailByUser = recipients.ToDictionary(r => r.PublicId, r => r.Email);

        foreach (var notification in batch)
        {
            ct.ThrowIfCancellationRequested();

            if (!emailByUser.TryGetValue(notification.RecipientUserPublicId, out var email)
                || string.IsNullOrWhiteSpace(email))
            {
                await MarkFailedAsync(db, notification,
                    "Destinatario sin correo configurado.", terminal: true, ct);
                continue;
            }

            try
            {
                // T116/T117 — Si hay template para el Type, úsala; sino fallback al HTML simple.
                var html = await templates.RenderHtmlAsync(
                    notification.Type,
                    BuildModel(notification, email),
                    ct);
                html ??= WrapAsHtml(notification.Subject, notification.Body);

                await sender.SendAsync(new EmailMessage(
                    To: email,
                    Subject: notification.Subject,
                    BodyHtml: html,
                    BodyText: notification.Body), ct);

                notification.EmailStatus = "Sent";
                notification.EmailSentAt = DateTime.UtcNow;
                notification.EmailAttemptCount++;
                notification.UpdatedBy = "EmailDispatcher";
            }
            catch (Exception ex)
            {
                notification.EmailAttemptCount++;
                var terminal = notification.EmailAttemptCount >= MaxAttempts;
                await MarkFailedAsync(db, notification, ex.Message, terminal, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static Task MarkFailedAsync(
        IApplicationDbContext db, Notification n, string reason, bool terminal, CancellationToken ct)
    {
        n.EmailStatus = terminal ? "Failed" : "Pending";
        n.UpdatedBy = "EmailDispatcher";
        db.NotificationDeliveryFailures.Add(new NotificationDeliveryFailure
        {
            NotificationId = n.Id,
            Channel = "Email",
            AttemptNumber = n.EmailAttemptCount,
            ErrorMessage = reason.Length > 2000 ? reason[..2000] : reason,
            FailedAt = DateTime.UtcNow,
            CreatedBy = "EmailDispatcher",
            UpdatedBy = "EmailDispatcher"
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Modelo plano para los placeholders <c>{{Key}}</c> de las plantillas
    /// (T116). El handler emisor pone los datos canónicos en Subject/Body;
    /// el dispatcher añade defaults razonables para los campos comunes.
    /// </summary>
    private static IReadOnlyDictionary<string, string?> BuildModel(
        Notification n, string email) =>
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Subject"] = n.Subject,
            ["Body"] = n.Body,
            ["UserName"] = email.Split('@')[0],
            ["Username"] = email,
            ["TenantName"] = n.TenantId.ToString(),
            ["ChangedAt"] = n.CreatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'"),
            ["AssignedAt"] = n.CreatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'"),
            ["LockedAt"] = n.CreatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'"),
            ["ResetAt"] = n.CreatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'"),
            ["DetectedAt"] = n.CreatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'"),
            ["LastIp"] = "—",
            ["UserAgent"] = "—",
            ["FailedAttempts"] = "—",
            ["AssignedBy"] = "Administrador",
            ["ApproverA"] = "—",
            ["ApproverB"] = "—",
            ["RoleName"] = "—",
            ["InvitedBy"] = "—",
            ["LoginUrl"] = "https://app.ingenia365.local"
        };

    private static string WrapAsHtml(string subject, string body)
    {
        var escapedSubject = System.Net.WebUtility.HtmlEncode(subject);
        var escapedBody = System.Net.WebUtility.HtmlEncode(body).Replace("\n", "<br/>");
        return $"<!DOCTYPE html><html><body><h3>{escapedSubject}</h3><p>{escapedBody}</p></body></html>";
    }
}
