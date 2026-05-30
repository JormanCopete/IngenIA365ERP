-- =============================================================================
-- File:        13d_Security_Mfa_Password_Policy.sql
-- Phase:       Fase 0 — Cimientos técnicos · US1 (Auth + MFA + Password policy)
-- Idempotent:  YES (CREATE TABLE / CREATE INDEX guarded with IF NOT EXISTS).
-- Author:      Fase 0 cimientos
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--   Las tablas SEC_* viven con el resto de la operación de cada cooperativa.
--
-- Crea las tablas que soportan:
--   - SEC_PasswordPolicies     (FR-008/009/010/011)
--   - SEC_PasswordHistory      (FR-010, append-only)
--   - SEC_MfaBackupCodes       (FR-013, hash BCrypt cost 11)
--   - SEC_MfaResetRequests     (FR-013, doble aprobación)
--
-- Convención: RowVersion en cada AuditableEntity (FR-049).
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i 13d_Security_Mfa_Password_Policy.sql
-- =============================================================================

SET NOCOUNT ON;
GO

-------------------------------------------------------------------------------
-- SEC_PasswordPolicies
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_PasswordPolicies')
BEGIN
    CREATE TABLE dbo.SEC_PasswordPolicies (
        Id                  INT             IDENTITY(1,1) NOT NULL,
        PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_PublicId DEFAULT NEWID(),
        TenantId            INT             NULL,
        MinLength           INT             NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_MinLength DEFAULT 12,
        RequireUppercase    BIT             NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_ReqUpper DEFAULT 1,
        RequireLowercase    BIT             NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_ReqLower DEFAULT 1,
        RequireDigit        BIT             NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_ReqDigit DEFAULT 1,
        RequireSymbol       BIT             NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_ReqSymb  DEFAULT 1,
        ExpiryDays          INT             NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_ExpDays  DEFAULT 90,
        HistorySize         INT             NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_HistSz   DEFAULT 5,
        LockoutThreshold    INT             NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_LockTh   DEFAULT 5,
        LockoutMinutes      INT             NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_LockMin  DEFAULT 15,
        CreatedAt           DATETIME2(0)    NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_Created  DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(100)   NULL,
        UpdatedAt           DATETIME2(0)    NULL,
        UpdatedBy           NVARCHAR(100)   NULL,
        IsDeleted           BIT             NOT NULL CONSTRAINT DF_SEC_PasswordPolicies_IsDeleted DEFAULT 0,
        DeletedAt           DATETIME2(0)    NULL,
        DeletedBy           NVARCHAR(100)   NULL,
        RowVersion          ROWVERSION      NOT NULL,
        CONSTRAINT PK_SEC_PasswordPolicies PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UX_SEC_PasswordPolicies_PublicId UNIQUE (PublicId),
        CONSTRAINT CK_SEC_PasswordPolicies_MinLength  CHECK (MinLength BETWEEN 8 AND 32),
        CONSTRAINT CK_SEC_PasswordPolicies_ExpDays    CHECK (ExpiryDays BETWEEN 30 AND 180),
        CONSTRAINT CK_SEC_PasswordPolicies_HistSize   CHECK (HistorySize BETWEEN 5 AND 24),
        CONSTRAINT CK_SEC_PasswordPolicies_LockTh     CHECK (LockoutThreshold BETWEEN 3 AND 10),
        CONSTRAINT CK_SEC_PasswordPolicies_LockMin    CHECK (LockoutMinutes BETWEEN 5 AND 60)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SEC_PasswordPolicies_TenantId_NotDeleted')
BEGIN
    CREATE UNIQUE INDEX UX_SEC_PasswordPolicies_TenantId_NotDeleted
        ON dbo.SEC_PasswordPolicies(TenantId)
        WHERE IsDeleted = 0;
END;
GO

-------------------------------------------------------------------------------
-- SEC_PasswordHistory (append-only)
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_PasswordHistory')
BEGIN
    CREATE TABLE dbo.SEC_PasswordHistory (
        Id              BIGINT          IDENTITY(1,1) NOT NULL,
        UserId          INT             NOT NULL,
        PasswordHash    NVARCHAR(500)   NOT NULL,
        SetAt           DATETIME2(0)    NOT NULL CONSTRAINT DF_SEC_PasswordHistory_SetAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SEC_PasswordHistory PRIMARY KEY CLUSTERED (Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SEC_PasswordHistory_User_SetAt')
BEGIN
    CREATE INDEX IX_SEC_PasswordHistory_User_SetAt
        ON dbo.SEC_PasswordHistory(UserId, SetAt DESC);
END;
GO

-------------------------------------------------------------------------------
-- SEC_MfaBackupCodes
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_MfaBackupCodes')
BEGIN
    CREATE TABLE dbo.SEC_MfaBackupCodes (
        Id              INT             IDENTITY(1,1) NOT NULL,
        PublicId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SEC_MfaBackupCodes_PublicId DEFAULT NEWID(),
        UserId          INT             NOT NULL,
        CodeHash        NVARCHAR(120)   NOT NULL,
        GeneratedAt     DATETIME2(0)    NOT NULL CONSTRAINT DF_SEC_MfaBackupCodes_Generated DEFAULT SYSUTCDATETIME(),
        UsedAt          DATETIME2(0)    NULL,
        BatchId         UNIQUEIDENTIFIER NOT NULL,
        CreatedAt       DATETIME2(0)    NOT NULL CONSTRAINT DF_SEC_MfaBackupCodes_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(100)   NULL,
        UpdatedAt       DATETIME2(0)    NULL,
        UpdatedBy       NVARCHAR(100)   NULL,
        IsDeleted       BIT             NOT NULL CONSTRAINT DF_SEC_MfaBackupCodes_IsDeleted DEFAULT 0,
        DeletedAt       DATETIME2(0)    NULL,
        DeletedBy       NVARCHAR(100)   NULL,
        RowVersion      ROWVERSION      NOT NULL,
        CONSTRAINT PK_SEC_MfaBackupCodes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UX_SEC_MfaBackupCodes_PublicId UNIQUE (PublicId)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SEC_MfaBackupCodes_User_Batch')
BEGIN
    CREATE INDEX IX_SEC_MfaBackupCodes_User_Batch
        ON dbo.SEC_MfaBackupCodes(UserId, BatchId);
END;
GO

IF NOT EXISTS (SELECT 1
    FROM sys.foreign_keys WHERE name = 'FK_SEC_MfaBackupCodes_User')
AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_Users')
BEGIN
    ALTER TABLE dbo.SEC_MfaBackupCodes
        ADD CONSTRAINT FK_SEC_MfaBackupCodes_User
            FOREIGN KEY (UserId) REFERENCES dbo.SEC_Users(Id);
END;
GO

-------------------------------------------------------------------------------
-- SEC_MfaResetRequests
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_MfaResetRequests')
BEGIN
    CREATE TABLE dbo.SEC_MfaResetRequests (
        Id                      INT             IDENTITY(1,1) NOT NULL,
        PublicId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SEC_MfaResetRequests_PublicId DEFAULT NEWID(),
        UserId                  INT             NOT NULL,
        RequestedBy             INT             NOT NULL,
        RequestedAt             DATETIME2(0)    NOT NULL CONSTRAINT DF_SEC_MfaResetRequests_Requested DEFAULT SYSUTCDATETIME(),
        Reason                  NVARCHAR(500)   NOT NULL,
        EvidenceAttachmentId    BIGINT          NULL,
        Status                  NVARCHAR(20)    NOT NULL CONSTRAINT DF_SEC_MfaResetRequests_Status DEFAULT 'Pending',
        FirstApproverId         INT             NULL,
        FirstApprovalAt         DATETIME2(0)    NULL,
        SecondApproverId        INT             NULL,
        SecondApprovalAt        DATETIME2(0)    NULL,
        ExecutedAt              DATETIME2(0)    NULL,
        ExpiresAt               DATETIME2(0)    NOT NULL,
        CreatedAt               DATETIME2(0)    NOT NULL CONSTRAINT DF_SEC_MfaResetRequests_Created DEFAULT SYSUTCDATETIME(),
        CreatedBy               NVARCHAR(100)   NULL,
        UpdatedAt               DATETIME2(0)    NULL,
        UpdatedBy               NVARCHAR(100)   NULL,
        IsDeleted               BIT             NOT NULL CONSTRAINT DF_SEC_MfaResetRequests_IsDeleted DEFAULT 0,
        DeletedAt               DATETIME2(0)    NULL,
        DeletedBy               NVARCHAR(100)   NULL,
        RowVersion              ROWVERSION      NOT NULL,
        CONSTRAINT PK_SEC_MfaResetRequests PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UX_SEC_MfaResetRequests_PublicId UNIQUE (PublicId),
        CONSTRAINT CK_SEC_MfaResetRequests_Status
            CHECK (Status IN ('Pending','Approved','Rejected','Executed','Expired')),
        CONSTRAINT CK_SEC_MfaResetRequests_DistinctApprovers
            CHECK (FirstApproverId IS NULL OR SecondApproverId IS NULL OR FirstApproverId <> SecondApproverId),
        CONSTRAINT CK_SEC_MfaResetRequests_NoSelfApprove
            CHECK ((FirstApproverId IS NULL  OR FirstApproverId  <> RequestedBy)
               AND (SecondApproverId IS NULL OR SecondApproverId <> RequestedBy))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SEC_MfaResetRequests_User_Status')
BEGIN
    CREATE INDEX IX_SEC_MfaResetRequests_User_Status
        ON dbo.SEC_MfaResetRequests(UserId, Status);
END;
GO

-------------------------------------------------------------------------------
-- Política global por defecto (TenantId NULL).
-- Solo se inserta si la tabla aún no tiene fila global, para idempotencia.
-------------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM dbo.SEC_PasswordPolicies
    WHERE TenantId IS NULL AND IsDeleted = 0)
BEGIN
    INSERT INTO dbo.SEC_PasswordPolicies
        (TenantId, MinLength, RequireUppercase, RequireLowercase,
         RequireDigit, RequireSymbol, ExpiryDays, HistorySize,
         LockoutThreshold, LockoutMinutes, CreatedBy, UpdatedBy, UpdatedAt)
    VALUES
        (NULL, 12, 1, 1, 1, 1, 90, 5, 5, 15, 'SYSTEM', 'SYSTEM', SYSUTCDATETIME());
END;
GO
