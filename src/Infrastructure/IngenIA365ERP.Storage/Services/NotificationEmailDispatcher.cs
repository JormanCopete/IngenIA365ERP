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
/// </summary>
public sealed class NotificationEmailDispatcher : BackgroundService
{
    private const int BatchSize = 25;
    private const int MaxAttempts = 4;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<NotificationEmailDispatcher> _logger;

    public NotificationEmailDispatcher(
        IServiceProvider services,
        ILogger<NotificationEmailDispatcher> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "NotificationEmailDispatcher iniciado — poll cada {Seconds}s, batch {Batch}.",
            PollInterval.TotalSeconds, BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOneBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "NotificationEmailDispatcher: error procesando batch; siguiente intento en {Seconds}s.",
                    PollInterval.TotalSeconds);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Un lote POR COOPERATIVA, no uno global.
    ///
    /// <para>
    /// Antes sacaba un solo <c>IApplicationDbContext</c> del contenedor y barria las
    /// notificaciones pendientes sin filtrar por cooperativa. Funcionaba por
    /// accidente: como todo vivia en <c>dbo</c>, "sin filtro" y "un solo esquema"
    /// coincidian. Con el aislamiento cableado, ese contexto —creado en un ambito
    /// sin peticion HTTP— resuelve a <c>dbo</c>, que ya no guarda las notificaciones
    /// de nadie. Encontraria cero filas y volveria a dormir quince segundos,
    /// indefinidamente y sin un solo error en el registro: el correo dejaria de
    /// salir y nada lo diria.
    /// </para>
    /// </summary>
    private async Task ProcessOneBatchAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var cooperativas = scope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var fabrica = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();

        foreach (var cooperativa in await CooperativasAsync(cooperativas, ct))
        {
            if (ct.IsCancellationRequested) return;

            await using var ambito = fabrica.Abrir(
                cooperativa.DatabaseName ?? cooperativa.SchemaName, cooperativa.ConnectionString);
            await ProcesarLoteDeAsync(ambito.Db, scope, cooperativa.Name, ct);
        }
    }

    /// <summary>
    /// Los esquemas de las cooperativas activas. Si el directorio falla, se registra
    /// y se devuelve vacio: el despachador reintenta al siguiente ciclo, y tumbar el
    /// servicio de fondo por un fallo transitorio de lectura seria peor.
    /// </summary>
    private async Task<IReadOnlyList<TenantDirectoryEntry>> CooperativasAsync(
        ITenantDirectory directorio, CancellationToken ct)
    {
        try
        {
            var todas = await directorio.ListActiveAsync(ct);
            return [.. todas.Where(c =>
                !string.IsNullOrWhiteSpace(c.DatabaseName) ||
                !string.IsNullOrWhiteSpace(c.ConnectionString))];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "No se pudieron enumerar las cooperativas para despachar correo. " +
                "Se reintenta en el siguiente ciclo.");
            return [];
        }
    }

    private async Task ProcesarLoteDeAsync(
        IApplicationDbContext db, IServiceScope scope, string esquema, CancellationToken ct)
    {
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var templates = scope.ServiceProvider.GetRequiredService<INotificationTemplateRenderer>();

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
