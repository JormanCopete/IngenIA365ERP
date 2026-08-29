using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Contexto del usuario autenticado en el flujo de identidad central
/// (Feature 002 — JWT emitido por <c>CentralJwtIssuer</c>). Espejo en
/// Application de los claims relevantes para los handlers — evita que
/// éstos toquen <c>HttpContext</c> directamente.
///
/// <para>
/// Coexiste con <see cref="ICurrentUserService"/> (Fase 0, SEC_Users
/// scoped al tenant). Cuando un endpoint usa el flujo central, los
/// handlers consumen <b>esta</b> interfaz; los endpoints legacy siguen
/// con la otra. Eventualmente, cuando US1+US2 reemplacen el login Fase
/// 0, <c>ICurrentUserService</c> se podrá derivar de aquí.
/// </para>
///
/// <para>
/// Si no hay autenticación válida con JWT central, <see cref="CentralUserId"/>
/// devuelve <c>null</c> — los handlers deben validarlo antes de operar.
/// </para>
/// </summary>
public interface ICurrentCentralUserContext
{
    /// <summary>Claim <c>sub</c> del JWT — Id del CentralUser en ADM_CentralUsers.</summary>
    Guid? CentralUserId { get; }

    /// <summary>Claim <c>email</c> — email del CentralUser en el momento de emisión del JWT.</summary>
    string? Email { get; }

    /// <summary>Claim <c>is_global_master_admin</c> — true si es admin del SaaS.</summary>
    bool IsGlobalMasterAdmin { get; }

    /// <summary>Claim <c>active_tenant_id</c> — Tenant.PublicId activo. <c>null</c>
    /// si el JWT es un challenge (purpose ≠ full) o si el usuario aún no eligió tenant.</summary>
    Guid? ActiveTenantPublicId { get; }

    /// <summary>Claim <c>tenant_admin</c> — true si es admin del tenant activo.
    /// Solo aplica cuando <see cref="ActiveTenantPublicId"/> está presente.</summary>
    bool TenantAdmin { get; }

    /// <summary>Claim <c>purpose</c> — <c>full</c> | <c>mfa-verify</c> | <c>mfa-enroll</c>
    /// | <c>tenant-select</c> | <c>password-reset</c>. Default <c>full</c> si no presente.</summary>
    string Purpose { get; }

    /// <summary>Claim <c>mfa_verified</c> — true si el MFA del usuario ya pasó en esta sesión.</summary>
    bool MfaVerified { get; }

    /// <summary>
    /// Claim <c>mfa_method</c> — con QUÉ método se superó el segundo factor en esta
    /// sesión. <c>Ninguno</c> cuando el claim no viene, que cubre tres casos con la
    /// misma respuesta: no tiene segundo factor, entró con un código de
    /// recuperación, o el token se emitió antes de que este claim existiera.
    ///
    /// <para>
    /// Es distinto de <see cref="MfaVerified"/>, que sólo dice SI se superó. Esa
    /// respuesta ya no basta: con dos métodos, una cooperativa puede aceptar uno y
    /// no el otro.
    /// </para>
    /// </summary>
    MetodosMfa MetodoMfa { get; }

    /// <summary>true si hay un JWT central válido autenticado (CentralUserId no es null).</summary>
    bool IsAuthenticated { get; }
}
