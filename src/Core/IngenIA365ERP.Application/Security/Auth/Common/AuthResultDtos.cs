namespace IngenIA365ERP.Application.Security.Auth.Common;

/// <summary>
/// Lo único que queda de los DTOs de autenticación de Fase 0.
///
/// <para>
/// Aquí vivían nueve records: los del login, la verificación del segundo factor,
/// la inscripción de MFA y los códigos de respaldo. Todos servían a endpoints
/// que operaban sobre <c>SEC_Users</c> y que la identidad central sustituyó; se
/// retiraron con ellos.
/// </para>
///
/// <para>
/// El reseteo administrativo de MFA con doble aprobación NO tiene equivalente en
/// la identidad central: es la única implementación que existe, sigue viva y por
/// eso sus dos records se quedan.
/// </para>
/// </summary>
public sealed record MfaResetRequestResult(Guid RequestPublicId, DateTime ExpiresAt);

public sealed record MfaResetApprovalResult(string Status);
