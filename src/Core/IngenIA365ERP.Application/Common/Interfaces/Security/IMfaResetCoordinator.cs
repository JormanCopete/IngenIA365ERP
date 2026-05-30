namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Coordina las solicitudes de reset administrativo de MFA con la regla de
/// doble aprobación (FR-016): el reset se ejecuta solo cuando dos
/// administradores distintos del solicitante aprueban dentro de 24 h.
/// </summary>
public interface IMfaResetCoordinator
{
    /// <summary>Crea la solicitud y devuelve su PublicId.</summary>
    Task<Guid> CreateRequestAsync(int requesterUserId, int targetUserId, string tenantId, CancellationToken ct);

    /// <summary>
    /// Registra una aprobación. Devuelve la cantidad de aprobadores distintos
    /// hasta el momento. Si el aprobador coincide con el solicitante o ya
    /// había aprobado, no incrementa.
    /// </summary>
    Task<int> ApproveAsync(Guid requestPublicId, int approverUserId, CancellationToken ct);

    Task<MfaResetRequestState?> GetAsync(Guid requestPublicId, CancellationToken ct);
}

public sealed record MfaResetRequestState(
    Guid PublicId,
    int RequesterUserId,
    int TargetUserId,
    string TenantId,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    IReadOnlyList<int> Approvers,
    bool IsExecuted);
