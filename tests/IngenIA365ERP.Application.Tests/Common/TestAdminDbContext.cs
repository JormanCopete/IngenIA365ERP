using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Common;

/// <summary>
/// AdminDbContext en memoria para tests de los handlers de US1 (invitaciones,
/// memberships). Mismo patrón que <see cref="TestApplicationDbContext"/>.
/// </summary>
public sealed class TestAdminDbContext : Microsoft.EntityFrameworkCore.DbContext, IAdminDbContext
{
    public TestAdminDbContext(DbContextOptions<TestAdminDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantBranch> TenantBranches => Set<TenantBranch>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<TenantMfaPolicy> TenantMfaPolicies => Set<TenantMfaPolicy>();
    public DbSet<TenantIdentityPolicy> TenantIdentityPolicies => Set<TenantIdentityPolicy>();
    public DbSet<DocumentHistoryEntry> DocumentHistory => Set<DocumentHistoryEntry>();
    public DbSet<IdentityCase> IdentityCases => Set<IdentityCase>();
    public DbSet<EmailChangeRequest> EmailChangeRequests => Set<EmailChangeRequest>();
    public DbSet<PlatformMfaPolicy> PlatformMfaPolicies => Set<PlatformMfaPolicy>();
    public DbSet<MfaRecoveryRequest> MfaRecoveryRequests => Set<MfaRecoveryRequest>();
    public DbSet<CentralUserLoginAttempt> CentralUserLoginAttempts => Set<CentralUserLoginAttempt>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<PromoContenido> PromoContenidos => Set<PromoContenido>();
    // Feature 013 (T0022, T0023): registro central de valores nacionales.
    public DbSet<PlatformNationalValue> PlatformNationalValues => Set<PlatformNationalValue>();
    public DbSet<PlatformRegulatoryDocument> PlatformRegulatoryDocuments => Set<PlatformRegulatoryDocument>();
    public DbSet<PlatformNationalValueCounter> PlatformNationalValueCounters => Set<PlatformNationalValueCounter>();

    public void DescartarCambios() => ChangeTracker.Clear();

    /// <summary>Como <c>AdminDbContext</c> (C68): los hechos de la central sólo se insertan.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await IngenIA365ERP.Persistence.DbContext.GuardaDeInmutabilidad.VerificarAsync(this, cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Subscription>();
        modelBuilder.Ignore<TenantSetting>();

        base.OnModelCreating(modelBuilder);

        // InMemory no soporta rowversion; lo ignoramos en todas las entidades
        // y emulamos concurrencia manualmente en los tests que la requieran.
        modelBuilder.Entity<Tenant>(b =>
        {
            b.Ignore(t => t.Subscriptions);
            b.Ignore(t => t.Settings);
            b.Ignore("RowVersion");
        });
        modelBuilder.Entity<TenantBranch>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<TenantMembership>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<Invitation>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<TenantMfaPolicy>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PlatformMfaPolicy>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<MfaRecoveryRequest>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<CentralUserLoginAttempt>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PasswordResetToken>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<UserSetting>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PromoContenido>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PlatformNationalValue>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PlatformRegulatoryDocument>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PlatformNationalValueCounter>(b => b.Ignore("RowVersion"));
    }

    public static TestAdminDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<TestAdminDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAdminDbContext(options);
    }
}
