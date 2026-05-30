using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.Configurations.Admin;
using IngenIA365ERP.Persistence.Configurations.Common;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.DbContext;

/// <summary>
/// DbContext de la BD <c>IngenIA365ERP_Admin</c> (cadena
/// <c>ConnectionStrings:TenantConnection</c>). Materializa SOLO las tablas
/// <c>ADM_*</c> — el resto del modelo operacional vive en
/// <see cref="ApplicationDbContext"/>.
///
/// Las entity configurations se aplican explícitamente para evitar que EF
/// arrastre el resto de configuraciones del assembly (Persistence cubre
/// ambos contextos).
/// </summary>
public class AdminDbContext : Microsoft.EntityFrameworkCore.DbContext, IAdminDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public AdminDbContext(
        DbContextOptions<AdminDbContext> options,
        ICurrentUserService? currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantBranch> TenantBranches => Set<TenantBranch>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");

        // Aplicación explícita de configuraciones (no `ApplyConfigurationsFromAssembly`
        // — eso traería las del modelo operacional también).
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new TenantBranchConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new TenantSettingConfiguration());

        // Extensión Tenant (NIT + campos legales) — coincide con migración 16.
        modelBuilder.ApplyConfiguration(new TenantLegalFieldsConfiguration());

        modelBuilder.ApplyBaseEntityConventions();

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var userName = _currentUserService?.UserName ?? "SYSTEM";

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is AuditableEntity addedAud)
            {
                addedAud.CreatedAt = now;
                addedAud.CreatedBy ??= userName;
                addedAud.UpdatedAt = now;
                addedAud.UpdatedBy ??= userName;
            }
            else if (entry.State == EntityState.Modified && entry.Entity is AuditableEntity modAud)
            {
                modAud.UpdatedAt = now;
                modAud.UpdatedBy = userName;
                entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
