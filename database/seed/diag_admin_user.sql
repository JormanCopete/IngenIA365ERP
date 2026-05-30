-- =============================================================================
-- File:        diag_admin_user.sql
-- Phase:       Fase 0 — Cimientos técnicos · US1 (diagnóstico del seed)
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--
-- Inspecciona el estado del admin del flujo Phase 0/US1 (no del usuario de
-- ASP.NET Identity legacy). Si el login devuelve Auth.InvalidCredentials,
-- corre este script para entender por qué.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i diag_admin_user.sql
-- =============================================================================

SET NOCOUNT ON;
GO

PRINT N'─ Filas en SEC_Users con username/email = admin@ingenia365.com';
SELECT
    Id,
    PublicId,
    Username,
    Email,
    LEFT(PasswordHash, 7)                              AS HashPrefix,        -- $2a$11 / $2b$11 …
    LEN(PasswordHash)                                  AS HashLen,
    IsActive,
    IsDeleted,
    IsMfaEnabled,
    MustChangePassword,
    FailedLoginAttempts,
    LockoutEndAt,
    IsSaasOperator,
    LastLoginAt,
    LastPasswordChangeAt,
    CreatedAt,
    CreatedBy,
    UpdatedAt,
    UpdatedBy,
    DeletedAt,
    DeletedBy
FROM dbo.SEC_Users
WHERE Username = N'admin@ingenia365.com'
   OR Email    = N'admin@ingenia365.com';
GO

PRINT N'';
PRINT N'─ Diagnóstico esperado para login OK con Admin@Temporal2024!:';
PRINT N'  · existe 1 fila';
PRINT N'  · HashPrefix = $2a$11  (BCrypt cost 11 — SC-008)';
PRINT N'  · IsActive = 1';
PRINT N'  · IsDeleted = 0';
PRINT N'  · LockoutEndAt = NULL  (o ya pasó)';
PRINT N'  · MustChangePassword = 0';
GO

PRINT N'';
PRINT N'─ Intentos de login recientes (últimos 10):';
SELECT TOP 10
    Id, Email, IpAddress, AttemptedAt, WasSuccessful, FailureReason
FROM dbo.SEC_LoginAttempts
WHERE Email = N'admin@ingenia365.com'
ORDER BY AttemptedAt DESC;
GO
