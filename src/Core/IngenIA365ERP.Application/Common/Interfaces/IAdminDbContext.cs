using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Interfaces;

/// <summary>
/// DbContext de la BD multi-tenant administrativa (<c>IngenIA365ERP_Admin</c>).
/// Contiene:
/// <list type="bullet">
///   <item><b>Catálogo SaaS</b> (Fase 0): tenants, branches, subscriptions, settings.</item>
///   <item><b>Identidad central</b> (feature 002): memberships, invitations, mfa
///         policies, login attempts (append-only).</item>
/// </list>
///
/// Las tablas operacionales por tenant (<c>SEC_*</c>, <c>COR_*</c>, etc.) viven
/// en <see cref="IApplicationDbContext"/> — esta interfaz NEVER se usa para
/// queries de datos operacionales.
///
/// <para>
/// El CRUD de la identidad central (usuarios, password, MFA) NO se opera vía
/// esta interfaz; se usa <c>ICentralIdentityProvider</c> de
/// <c>Common.Abstractions</c> (Chunk C), que envuelve internamente el
/// <c>UserManager&lt;CentralUserIdentity&gt;</c> de ASP.NET Identity.
/// Las DbSets de aquí son sólo para queries de lectura/escritura de las
/// entidades de Domain (memberships, invitations, etc.).
/// </para>
/// </summary>
public interface IAdminDbContext
{
    // --- Catálogo SaaS ---
    DbSet<Tenant> Tenants { get; }
    DbSet<TenantBranch> TenantBranches { get; }

    // --- Identidad central ---
    DbSet<TenantMembership> TenantMemberships { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<TenantMfaPolicy> TenantMfaPolicies { get; }
    DbSet<PlatformMfaPolicy> PlatformMfaPolicies { get; }
    DbSet<CentralUserLoginAttempt> CentralUserLoginAttempts { get; }

    // --- Phase 4b (Profile & Recovery) ---
    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    // --- Preferencias de interfaz por usuario ---
    DbSet<UserSetting> UserSettings { get; }

    // --- Contenido promocional de la pantalla de inicio de sesion ---
    DbSet<PromoContenido> PromoContenidos { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
