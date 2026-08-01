namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>Resultado de un intento de login registrado en <c>ADM_CentralUserLoginAttempts</c>.</summary>
public enum LoginAttemptResult
{
    Success = 0,
    InvalidPassword = 1,
    UserNotFound = 2,
    MfaRequired = 3,
    MfaInvalid = 4,
    LockedOut = 5,
    Disabled = 6,
    Other = 99,
}
