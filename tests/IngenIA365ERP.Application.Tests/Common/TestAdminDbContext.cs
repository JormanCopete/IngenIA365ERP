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
    public DbSet<CentralUserLoginAttempt> CentralUserLoginAttempts => Set<CentralUserLoginAttempt>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

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
        modelBuilder.Entity<CentralUserLoginAttempt>(b => b.Ignore("RowVersion"));
        modelBuilder.Entity<PasswordResetToken>(b => b.Ignore("RowVersion"));
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
