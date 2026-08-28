using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.Configurations.Admin;
using IngenIA365ERP.Persistence.Configurations.Common;
using IngenIA365ERP.Persistence.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
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
///
/// <para>
/// <b>Tercera responsabilidad: el llavero de DataProtection.</b> Implementa
/// <see cref="IDataProtectionKeyContext"/>, así que las claves con las que se
/// cifran el secreto TOTP de cada persona y la clave de cada adjunto viven en
/// esta misma base.
/// </para>
///
/// <para>
/// Antes no vivían en ninguna parte: <c>AddDataProtection()</c> se registraba sin
/// <c>PersistKeysTo*</c>, así que el llavero caía en el perfil del proceso —en un
/// contenedor Linux, dentro de su capa escribible—. Con dos réplicas de API eso
/// significa dos llaveros distintos: lo que cifra un pod, el otro no lo abre. En
/// el segundo factor se ve como «código inválido» de forma intermitente; en los
/// adjuntos, como archivos que dejan de poder leerse. Y cada despliegue rota los
/// pods, o sea que empieza de cero.
/// </para>
///
/// <para>
/// Se eligió la base y no Redis porque las claves entran así en los respaldos que
/// ya existen (WAL continuo + volcado diario) y se restauran junto con los datos
/// que protegen. Contrapartida a saber: quien pueda leer esta base tiene el
/// llavero y el ciphertext a la vez, así que el cifrado en columna deja de valer
/// contra un atacante con acceso a la base. Protege contra respaldos filtrados y
/// contra volcados parciales, no contra eso.
/// </para>
/// </summary>
public class AdminDbContext
    : IdentityDbContext<CentralUserIdentity, IdentityRole<Guid>, Guid>,
      IAdminDbContext,
      IDataProtectionKeyContext
{
    private readonly ICurrentUserService? _currentUserService;

    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public AdminDbContext(
        DbContextOptions<AdminDbContext> options,
        ICurrentUserService? currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Llavero de DataProtection (tabla <c>ADM_DataProtectionKeys</c>). Lo lee y
    /// lo escribe el propio framework; no se toca desde el código de la casa.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // --- Catálogo SaaS (Fase 0) ---
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantBranch> TenantBranches => Set<TenantBranch>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();

    // --- Identidad central (feature 002) ---
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<TenantMfaPolicy> TenantMfaPolicies => Set<TenantMfaPolicy>();
    public DbSet<PlatformMfaPolicy> PlatformMfaPolicies => Set<PlatformMfaPolicy>();

    /// <summary>
    /// Credenciales de segundo factor, una tabla con discriminador. Sustituye a la
    /// columna única <c>ADM_CentralUsers.MfaSecret</c>, que se conserva mientras
    /// dure el modo compatibilidad.
    /// </summary>
    public DbSet<MfaCredential> MfaCredentials => Set<MfaCredential>();
    public DbSet<CentralUserLoginAttempt> CentralUserLoginAttempts => Set<CentralUserLoginAttempt>();
    // --- Identidad central · Phase 4b (Profile & Recovery) ---
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<PromoContenido> PromoContenidos => Set<PromoContenido>();
    // Nota: DbSet<CentralUserIdentity> Users es heredado de IdentityDbContext;
    // no se expone via IAdminDbContext porque CentralUserIdentity es tipo de
    // Infrastructure (Application usa ICentralIdentityProvider).

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // OBLIGATORIO llamar a base ANTES de aplicar configs propias: registra
        // los tipos de ASP.NET Identity (IdentityUserRole, IdentityUserClaim, etc.).
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("dbo");

        // Llavero de DataProtection. La entidad la aporta el framework; lo único
        // que se le impone es el prefijo de módulo de la casa.
        modelBuilder.Entity<DataProtectionKey>().ToTable("ADM_DataProtectionKeys");

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

        // Jerarquía TPH: la raíz declara tabla, discriminador, índices y filtro;
        // el subtipo sólo aporta sus columnas propias. Las dos se registran.
        modelBuilder.ApplyConfiguration(new MfaCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new TotpCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new WebAuthnCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new CentralUserLoginAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordResetTokenConfiguration());

        // Preferencias de interfaz por usuario. Este contexto NO usa
        // ApplyConfigurationsFromAssembly, así que una configuration que no se
        // registre acá simplemente no existe para EF y la tabla nunca aparece
        // en la migración.
        modelBuilder.ApplyConfiguration(new UserSettingConfiguration());
        modelBuilder.ApplyConfiguration(new PromoContenidoConfiguration());

        // Feature 004: columnas de interoperabilidad con el store multitenant.
        // TenantDbContext (Finbuckle/ErpTenantInfo) mapea LA MISMA tabla
        // ADM_Tenants con estas columnas adicionales; como este contexto es el
        // dueño de las migraciones (MigrationsTarget.Admin), deben existir en
        // su modelo aunque la entidad Admin Tenant no las use (shadow props).
        modelBuilder.Entity<Tenant>(b =>
        {
            b.Property<string?>("Identifier").HasMaxLength(100);
            b.Property<string?>("ConnectionString").HasMaxLength(500);
            b.Property<string?>("LicenseType").HasMaxLength(50);
            b.Property<DateTime?>("ExpirationDate");
        });

        modelBuilder.ApplyBaseEntityConventions(Database.ProviderName);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Feature 004 (D-06): misma convencion de precision que el contexto
        // operativo para paridad entre motores.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>
    /// Asigna una propiedad SOMBRA solo si viene vacia. Son columnas que
    /// existen en la tabla pero no en la entidad, asi que ningun handler puede
    /// tocarlas ni notar que faltan.
    /// </summary>
    private static void RellenarSiVacia(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        string propiedadSombra,
        string valor)
    {
        var propiedad = entry.Property(propiedadSombra);
        if (string.IsNullOrWhiteSpace(propiedad.CurrentValue as string))
            propiedad.CurrentValue = valor;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var userName = _currentUserService?.UserName ?? "SYSTEM";

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                // Identifier es propiedad SOMBRA: la declara este contexto para
                // que exista la columna, pero no esta en la entidad Tenant, asi
                // que ningun handler puede asignarla. TenantDbContext (Finbuckle)
                // mapea LA MISMA tabla y la lee como NO nulable, de modo que una
                // cooperativa creada por la aplicacion dejaba la columna en NULL
                // y la siguiente lectura de Finbuckle reventaba con
                // InvalidCastException al arrancar, antes de servir nada.
                //
                // Se rellena aca y no en cada handler justamente porque es
                // invisible desde el modelo: quien escriba el proximo camino de
                // alta no tiene forma de saber que existe.
                if (entry.Entity is Tenant tenantNuevo)
                {
                    // Las cuatro que ErpTenantInfo declara obligatorias en
                    // TenantDbContext.OnModelCreating son Identifier, Name,
                    // SchemaName y LicenseType. Name y SchemaName ya son NOT NULL
                    // en la tabla; las otras dos son sombra y quedaban vacias.
                    RellenarSiVacia(entry, "Identifier",
                        !string.IsNullOrWhiteSpace(tenantNuevo.Subdomain)
                            ? tenantNuevo.Subdomain
                            : tenantNuevo.SchemaName);

                    RellenarSiVacia(entry, "LicenseType",
                        !string.IsNullOrWhiteSpace(tenantNuevo.PlanType)
                            ? tenantNuevo.PlanType
                            : "Basic");
                }
                
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
