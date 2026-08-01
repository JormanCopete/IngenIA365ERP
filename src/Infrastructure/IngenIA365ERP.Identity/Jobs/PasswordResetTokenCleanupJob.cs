using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Jobs;

/// <summary>
/// T122 — Background job complementario al <see cref="InvitationExpiryJob"/>.
/// Purga tokens de reset de contraseña consumidos o expirados con más de 30
/// días de antigüedad (data-model §6). Los tokens recientes se conservan
/// para análisis forense de incidentes.
/// </summary>
public sealed class PasswordResetTokenCleanupJob(
    IServiceScopeFactory scopeFactory,
    ILogger<PasswordResetTokenCleanupJob> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(30);
    private const int BatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "PasswordResetTokenCleanupJob falló; reintenta en {Interval}.", TickInterval);
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

        var threshold = DateTime.UtcNow - RetentionWindow;
        var totalDeleted = 0;

        while (!ct.IsCancellationRequested)
        {
            // Tokens consumidos > 30d O expirados > 30d.
            var batch = await db.PasswordResetTokens
                .Where(t => (t.ConsumedAt != null && t.ConsumedAt < threshold)
                         || (t.ExpiresAt < threshold && t.ConsumedAt == null))
                .OrderBy(t => t.Id)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batch.Count == 0) break;

            db.PasswordResetTokens.RemoveRange(batch);
            await db.SaveChangesAsync(ct);
            totalDeleted += batch.Count;
        }

        if (totalDeleted > 0)
        {
            logger.LogInformation(
                "PasswordResetTokenCleanupJob: purgados {Count} tokens (> {Days} días).",
                totalDeleted, (int)RetentionWindow.TotalDays);
        }
    }
}
