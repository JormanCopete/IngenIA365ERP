using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.Configurations.Admin;
using IngenIA365ERP.Persistence.Configurations.Common;
using IngenIA365ERP.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.DbContext;

/// <summary>
/// DbContext de la BD <c>IngenIA365ERP_Admin</c>. Combina dos responsabilidades:
/// <list type="bullet">
///   <item><b>Catálogo SaaS</b> (Fase 0): tenants, branches, subscriptions, settings.</item>
///   <item><b>Identidad central</b> (feature 002): hereda
///         <see cref="IdentityDbContext{TUser,TRole,TKey}"/> para que ASP.NET Core
///         Identity persista <see cref="CentralUserIdentity"/> en las tablas
///         <c>ADM_CentralUsers</c> / <c>ADM_Roles</c> / etc. (renombradas en
///         <see cref="OnModelCreating"/>). Más las entidades de membership,
///         invitation, mfa policy y login attempts.</item>
/// </list>
///
/// Las configurations se aplican explícitamente para no arrastrar las del modelo
/// operacional (que vive en <see cref="ApplicationDbContext"/>).
/// </summary>
public class AdminDbContext : IdentityDbContext<CentralUserIdentity, IdentityRole<Guid>, Guid>, IAdminDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public AdminDbContext(
        DbContextOptions<AdminDbContext> options,
        ICurrentUserService? currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    // --- Catálogo SaaS (Fase 0) ---
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantBranch> TenantBranches => Set<TenantBranch>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();

    // --- Identidad central (feature 002) ---
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<TenantMfaPolicy> TenantMfaPolicies => Set<TenantMfaPolicy>();
    public DbSet<CentralUserLoginAttempt> CentralUserLoginAttempts => Set<CentralUserLoginAttempt>();
    // --- Identidad central · Phase 4b (Profile & Recovery) ---
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    // Nota: DbSet<CentralUserIdentity> Users es heredado de IdentityDbContext;
    // no se expone via IAdminDbContext porque CentralUserIdentity es tipo de
    // Infrastructure (Application usa ICentralIdentityProvider).

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // OBLIGATORIO llamar a base ANTES de aplicar configs propias: registra
        // los tipos de ASP.NET Identity (IdentityUserRole, IdentityUserClaim, etc.).
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("dbo");

        // Catálogo SaaS (Fase 0).
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new TenantBranchConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new TenantSettingConfiguration());
        modelBuilder.ApplyConfiguration(new TenantLegalFieldsConfiguration());

        // Identidad central (feature 002).
        modelBuilder.ApplyConfiguration(new CentralUserIdentityConfiguration());
        modelBuilder.RenameIdentityTablesToAdminPrefix();
        modelBuilder.ApplyConfiguration(new TenantMembershipConfiguration());
        modelBuilder.ApplyConfiguration(new InvitationConfiguration());
        modelBuilder.ApplyConfiguration(new TenantMfaPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new CentralUserLoginAttemptConfiguration());

        modelBuilder.ApplyBaseEntityConventions();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var userName = _currentUserService?.UserName ?? "SYSTEM";

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is AuditableEntity addedAud)
                {
                    addedAud.CreatedAt = now;
                    addedAud.CreatedBy ??= userName;
                    addedAud.UpdatedAt = now;
                    addedAud.UpdatedBy ??= userName;
                }
                else if (entry.Entity is CentralUserIdentity addedIdentity)
                {
                    addedIdentity.CreatedAt = now;
                    addedIdentity.CreatedBy ??= userName;
                    addedIdentity.UpdatedAt = now;
                    addedIdentity.UpdatedBy ??= userName;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is AuditableEntity modAud)
                {
                    modAud.UpdatedAt = now;
                    modAud.UpdatedBy = userName;
                    entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
                }
                else if (entry.Entity is CentralUserIdentity modIdentity)
                {
                    modIdentity.UpdatedAt = now;
                    modIdentity.UpdatedBy = userName;
                    entry.Property(nameof(CentralUserIdentity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(CentralUserIdentity.CreatedBy)).IsModified = false;
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
