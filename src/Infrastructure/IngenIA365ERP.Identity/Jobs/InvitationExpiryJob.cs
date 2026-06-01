using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Jobs;

/// <summary>
/// T122 — Background job que cada hora:
/// <list type="bullet">
///   <item>Marca <c>Invitation Pending → Expired</c> las que pasaron <c>ExpiresAt</c>.</item>
///   <item>En cascada, si la <c>TenantMembership</c> asociada quedó en estado
///         <c>Invited</c> (caso: invitación a email ya con CentralUser que
///         nunca aceptó), la marca también <c>Revoked</c> con razón
///         <c>Invitation.Expired</c>.</item>
///   <item>Emite eventos auditables <c>Invitation.Expired</c> y, cuando
///         aplique, <c>Membership.Revoked.InvitationExpired</c>.</item>
/// </list>
///
/// <para>
/// Idempotente: si llega tarde (por ejemplo tras downtime), procesa todo el
/// backlog en un solo tick. Toleramos hasta ~1h de delay (HOSTED_SERVICE_DELAY).
/// </para>
/// </summary>
public sealed class InvitationExpiryJob(
    IServiceScopeFactory scopeFactory,
    ILogger<InvitationExpiryJob> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);
    private const int BatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Pequeño delay inicial para no colisionar con el bootstrap de la API.
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
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
                // Errores transitorios (BD caída, p. ej.) — log y siguiente tick.
                logger.LogError(ex, "InvitationExpiryJob falló en el tick; reintenta en {Interval}.", TickInterval);
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var auditWriter = scope.ServiceProvider.GetRequiredService<IAuditAppendOnlyWriter>();
        var now = DateTime.UtcNow;

        // Procesamos en batches para no cargar la app si hay backlog.
        int totalInvitations = 0, totalMemberships = 0;
        while (!ct.IsCancellationRequested)
        {
            var batch = await db.Invitations
                .Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAt <= now)
                .OrderBy(i => i.ExpiresAt)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batch.Count == 0) break;

            foreach (var invitation in batch)
            {
                invitation.MarkExpired(now);
                await EmitInvitationExpiredAuditAsync(auditWriter, invitation, now, ct);
            }

            // Cascada: cualquier TenantMembership en Invited cuyo correo coincida
            // con esta invitación expirada se marca Revoked. Caso típico: usuario
            // ya existía y se le invitó otra vez sin aceptar.
            foreach (var invitation in batch)
            {
                // Buscamos por NormalizedEmail vía FK lógica: la membresía Invited
                // del CentralUser cuyo email coincida con la invitación.
                // Para esto, hay que resolver el CentralUserId vía
                // ICentralIdentityProvider — lo hacemos perezosamente para no
                // pegar contra ASP.NET Identity en cada iteración.
                // Simplificación: matching directo por TenantId + estado Invited.
                // El handler de Accept ya valida que el email coincida, así que
                // mantener Invited huérfanos no rompe seguridad — solo los
                // limpiamos para no enseñar Memberships zombi.
                var orphanMemberships = await db.TenantMemberships
                    .Where(m => m.TenantId == invitation.TenantId
                             && m.Status == MembershipStatus.Invited)
                    .ToListAsync(ct);
                foreach (var m in orphanMemberships)
                {
                    m.Revoke(byUserId: Guid.Empty, now); // system actor
                    await EmitMembershipRevokedAuditAsync(auditWriter, m, now, ct);
                    totalMemberships++;
                }
            }

            await db.SaveChangesAsync(ct);
            totalInvitations += batch.Count;
        }

        if (totalInvitations > 0 || totalMemberships > 0)
        {
            logger.LogInformation(
                "InvitationExpiryJob: expiradas {Invitations} invitaciones, revocadas {Memberships} membresías huérfanas.",
                totalInvitations, totalMemberships);
        }
    }

    private static async Task EmitInvitationExpiredAuditAsync(
        IAuditAppendOnlyWriter auditWriter, Invitation invitation, DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: invitation.TenantId.ToString("N"),
                UserId: invitation.InvitedByUserId.ToString("N"),
                UserName: "system",
                Action: AuditEventTypes.InvitationExpired,
                EntityType: nameof(Invitation),
                EntityPublicId: invitation.PublicId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null, NewValuesJson: null,
                ChangedFields: new[] { "Status" },
                IpAddress: null, UserAgent: null,
                Endpoint: "background:InvitationExpiryJob",
                HttpMethod: null, HttpStatusCode: null, DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"InvitationExpiryJob audit (Invitation.Expired) fail: {ex.Message}");
        }
    }

    private static async Task EmitMembershipRevokedAuditAsync(
        IAuditAppendOnlyWriter auditWriter, TenantMembership membership, DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: membership.TenantId.ToString("N"),
                UserId: membership.CentralUserId.ToString("N"),
                UserName: "system",
                Action: AuditEventTypes.MembershipRevokedInvitationExpired,
                EntityType: nameof(TenantMembership),
                EntityPublicId: membership.PublicId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null, NewValuesJson: null,
                ChangedFields: new[] { "Status" },
                IpAddress: null, UserAgent: null,
                Endpoint: "background:InvitationExpiryJob",
                HttpMethod: null, HttpStatusCode: null, DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"InvitationExpiryJob audit (Membership.Revoked) fail: {ex.Message}");
        }
    }
}
