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
    DbSet<CentralUserLoginAttempt> CentralUserLoginAttempts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
