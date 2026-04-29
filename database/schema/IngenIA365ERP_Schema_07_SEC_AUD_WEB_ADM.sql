-- ============================================================
-- IngenIA365ERP — Database Schema v1.0
-- Modules: SEC_ (11), AUD_ (14), WEB_ (6), ADM_ (3) — 34 tables
-- ============================================================
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- MODULE: SEC_ — Security (11 tables)
-- ============================================================

-- 236. SEC_Users (original: sys_sasusu)
CREATE TABLE SEC_Users (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    PersonId                INT NULL,
    Username                NVARCHAR(50) NOT NULL,
    Email                   NVARCHAR(200) NULL,
    PasswordHash            NVARCHAR(500) NOT NULL,
    PasswordSalt            NVARCHAR(200) NULL,
    IsActive                BIT NOT NULL DEFAULT 1,
    IsEmailVerified         BIT NOT NULL DEFAULT 0,
    IsMfaEnabled            BIT NOT NULL DEFAULT 0,
    MfaSecret               NVARCHAR(200) NULL,
    LastLoginAt             DATETIME2 NULL,
    LastPasswordChangeAt    DATETIME2 NULL,
    FailedLoginAttempts     INT NOT NULL DEFAULT 0,
    LockoutEndAt            DATETIME2 NULL,
    LegacyLogin             NVARCHAR(20) NULL,
    LegacyPassword          NVARCHAR(30) NULL,
    CanApproveLoansMin      DECIMAL(18,2) NOT NULL DEFAULT 0,
    CanApproveLoansMax      DECIMAL(18,2) NOT NULL DEFAULT 0,
    CanOverrideLimits       BIT NOT NULL DEFAULT 0,
    IdentificationNumber    NVARCHAR(20) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_SEC_Users PRIMARY KEY (Id),
    CONSTRAINT UK_SEC_Users_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_SEC_Users_Username UNIQUE (Username)
);
GO

-- 237. SEC_UserMenuAccess (original: sys_menusu)
CREATE TABLE SEC_UserMenuAccess (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    UserId                  INT NOT NULL,
    MenuType                INT NULL,
    ProgramType             NVARCHAR(2) NULL,
    ProgramCode             NVARCHAR(30) NULL,
    ProgramName             NVARCHAR(100) NULL,
    HasAccess               BIT NOT NULL DEFAULT 1,
    MenuCode                NVARCHAR(30) NULL,
    SubMenuCode             NVARCHAR(30) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_SEC_UserMenuAccess PRIMARY KEY (Id),
    CONSTRAINT UK_SEC_UserMenuAccess_PublicId UNIQUE (PublicId)
);
GO

-- 238. SEC_Modules (original: sys_programa)
CREATE TABLE SEC_Modules (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    UserId                  INT NULL,
    ProgramCode             NVARCHAR(30) NULL,
    Description             NVARCHAR(100) NULL,
    ProgramType             NVARCHAR(2) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_SEC_Modules PRIMARY KEY (Id),
    CONSTRAINT UK_SEC_Modules_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_SEC_Modules_UserProgram UNIQUE (UserId, ProgramCode)
);
GO

-- 239. SEC_UserAssignments (original: sys_ComAsigna)
CREATE TABLE SEC_UserAssignments (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    VoucherTypeCode         NVARCHAR(10) NULL,
    UserId                  INT NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_SEC_UserAssignments PRIMARY KEY (Id),
    CONSTRAINT UK_SEC_UserAssignments_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_SEC_UserAssignments_VoucherUser UNIQUE (VoucherTypeCode, UserId)
);
GO

-- 240. SEC_Roles (nueva)
CREATE TABLE SEC_Roles (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Name                    NVARCHAR(100) NOT NULL,
    Description             NVARCHAR(500) NULL,
    IsSystemRole            BIT NOT NULL DEFAULT 0,
    IsActive                BIT NOT NULL DEFAULT 1,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_SEC_Roles PRIMARY KEY (Id),
    CONSTRAINT UK_SEC_Roles_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_SEC_Roles_Name UNIQUE (Name)
);
GO

-- 241. SEC_Permissions (nueva)
CREATE TABLE SEC_Permissions (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Resource                NVARCHAR(100) NOT NULL,
    Action                  NVARCHAR(50) NOT NULL,
    Description             NVARCHAR(200) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_SEC_Permissions PRIMARY KEY (Id),
    CONSTRAINT UK_SEC_Permissions_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_SEC_Permissions_ResourceAction UNIQUE (Resource, Action)
);
GO

-- 242. SEC_RolePermissions (nueva)
CREATE TABLE SEC_RolePermissions (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    RoleId                  INT NOT NULL,
    PermissionId            INT NOT NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_SEC_RolePermissions PRIMARY KEY (Id),
    CONSTRAINT UK_SEC_RolePermissions_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_SEC_RolePermissions_RolePerm UNIQUE (RoleId, PermissionId)
);
GO

-- 243. SEC_UserRoles (nueva)
CREATE TABLE SEC_UserRoles (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    UserId                  INT NOT NULL,
    RoleId                  INT NOT NULL,
    AssignedAt              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    AssignedBy              NVARCHAR(100) NOT NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_SEC_UserRoles PRIMARY KEY (Id),
    CONSTRAINT UK_SEC_UserRoles_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_SEC_UserRoles_UserRole UNIQUE (UserId, RoleId)
);
GO

-- 244. SEC_RefreshTokens (nueva)
CREATE TABLE SEC_RefreshTokens (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    UserId                  INT NOT NULL,
    Token                   NVARCHAR(500) NOT NULL,
    ExpiresAt               DATETIME2 NOT NULL,
    RevokedAt               DATETIME2 NULL,
    RevokedBy               NVARCHAR(100) NULL,
    ReplacedByToken         NVARCHAR(500) NULL,
    IpAddress               NVARCHAR(50) NULL,
    UserAgent               NVARCHAR(500) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_SEC_RefreshTokens PRIMARY KEY (Id),
    CONSTRAINT UK_SEC_RefreshTokens_PublicId UNIQUE (PublicId)
);
GO

-- 245. SEC_UserSessions (nueva)
CREATE TABLE SEC_UserSessions (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    UserId                  INT NOT NULL,
    SessionToken            NVARCHAR(500) NOT NULL,
    IpAddress               NVARCHAR(50) NULL,
    UserAgent               NVARCHAR(500) NULL,
    StartedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastActivityAt          DATETIME2 NULL,
    EndedAt                 DATETIME2 NULL,
    IsActive                BIT NOT NULL DEFAULT 1,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_SEC_UserSessions PRIMARY KEY (Id),
    CONSTRAINT UK_SEC_UserSessions_PublicId UNIQUE (PublicId)
);
GO

-- 246. SEC_LoginAttempts (nueva)
CREATE TABLE SEC_LoginAttempts (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Email                   NVARCHAR(200) NULL,
    IpAddress               NVARCHAR(50) NULL,
    UserAgent               NVARCHAR(500) NULL,
    AttemptedAt             DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    WasSuccessful           BIT NOT NULL,
    FailureReason           NVARCHAR(200) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_SEC_LoginAttempts PRIMARY KEY (Id),
    CONSTRAINT UK_SEC_LoginAttempts_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- MODULE: AUD_ — Audit (14 tables, all BIGINT PK)
-- ============================================================

-- 247. AUD_CompanyChanges (original: sys_ciaaud)
CREATE TABLE AUD_CompanyChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                INT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_CompanyChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_CompanyChanges_PublicId UNIQUE (PublicId)
);
GO

-- 248. AUD_UserChanges (original: sys_sasusuaud)
CREATE TABLE AUD_UserChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                INT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_UserChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_UserChanges_PublicId UNIQUE (PublicId)
);
GO

-- 249. AUD_VoucherTypeChanges (original: sys_compro02aud)
CREATE TABLE AUD_VoucherTypeChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                INT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_VoucherTypeChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_VoucherTypeChanges_PublicId UNIQUE (PublicId)
);
GO

-- 250. AUD_MasterChanges (original: sys_masaud)
CREATE TABLE AUD_MasterChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                INT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_MasterChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_MasterChanges_PublicId UNIQUE (PublicId)
);
GO

-- 251. AUD_MenuChanges (original: sys_menuaud)
CREATE TABLE AUD_MenuChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                INT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_MenuChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_MenuChanges_PublicId UNIQUE (PublicId)
);
GO

-- 252. AUD_PeriodChanges (original: sys_periodoAud)
CREATE TABLE AUD_PeriodChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                INT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_PeriodChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_PeriodChanges_PublicId UNIQUE (PublicId)
);
GO

-- 253. AUD_AssignmentChanges (original: sys_ComAsignaAud)
CREATE TABLE AUD_AssignmentChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                INT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_AssignmentChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_AssignmentChanges_PublicId UNIQUE (PublicId)
);
GO

-- 254. AUD_AccountChanges (original: cnt_maecuenAud)
CREATE TABLE AUD_AccountChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                INT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_AccountChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_AccountChanges_PublicId UNIQUE (PublicId)
);
GO

-- 255. AUD_JournalChanges (original: cnt_movaud)
CREATE TABLE AUD_JournalChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                BIGINT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_JournalChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_JournalChanges_PublicId UNIQUE (PublicId)
);
GO

-- 256. AUD_PortfolioTransactionChanges (original: cop_movaud)
CREATE TABLE AUD_PortfolioTransactionChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                BIGINT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_PortfolioTransactionChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_PortfolioTransactionChanges_PublicId UNIQUE (PublicId)
);
GO

-- 257. AUD_PortfolioMasterChanges (original: cop_mcaaud)
CREATE TABLE AUD_PortfolioMasterChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                INT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_PortfolioMasterChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_PortfolioMasterChanges_PublicId UNIQUE (PublicId)
);
GO

-- 258. AUD_DefaultChanges (original: cop_moraud)
CREATE TABLE AUD_DefaultChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                INT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_DefaultChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_DefaultChanges_PublicId UNIQUE (PublicId)
);
GO

-- 259. AUD_SavingsChanges (original: cop_ahoraud)
CREATE TABLE AUD_SavingsChanges (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Action                  NVARCHAR(10) NOT NULL,
    ActionDate              DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    IpAddress               NVARCHAR(50) NULL,
    EntityId                INT NULL,
    OldValues               NVARCHAR(MAX) NULL,
    NewValues               NVARCHAR(MAX) NULL,
    AdditionalInfo          NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_SavingsChanges PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_SavingsChanges_PublicId UNIQUE (PublicId)
);
GO

-- 260. AUD_AuditReferences (nueva)
CREATE TABLE AUD_AuditReferences (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    EntityType              NVARCHAR(100) NOT NULL,
    EntityId                BIGINT NOT NULL,
    Action                  NVARCHAR(10) NOT NULL,
    UserId                  INT NULL,
    UserName                NVARCHAR(100) NULL,
    Timestamp               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ExternalDocumentId      NVARCHAR(100) NULL,
    Summary                 NVARCHAR(500) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_AUD_AuditReferences PRIMARY KEY (Id),
    CONSTRAINT UK_AUD_AuditReferences_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- MODULE: WEB_ — Web Portal (6 tables, all BIGINT PK)
-- ============================================================

-- 261. WEB_LoanApplications (original: web_solcred)
CREATE TABLE WEB_LoanApplications (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    PersonId                INT NULL,
    SequenceNumber          INT NULL,
    ApplicationDate         DATE NULL,
    IpAddress               NVARCHAR(50) NULL,
    CreditLineId            INT NULL,
    InterestRate            DECIMAL(10,5) NULL,
    RequestedAmount         DECIMAL(18,2) NULL,
    TermMonths              INT NULL,
    Periodicity             NVARCHAR(2) NULL,
    InstallmentAmount       DECIMAL(18,2) NULL,
    Purpose                 NVARCHAR(300) NULL,
    LegacyCodigoTer         NVARCHAR(20) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_WEB_LoanApplications PRIMARY KEY (Id),
    CONSTRAINT UK_WEB_LoanApplications_PublicId UNIQUE (PublicId)
);
GO

-- 262. WEB_AuxiliaryApplications (original: web_solaux)
CREATE TABLE WEB_AuxiliaryApplications (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    PersonId                INT NULL,
    SequenceNumber          INT NULL,
    ApplicationDate         DATE NULL,
    IpAddress               NVARCHAR(50) NULL,
    SubsidyType             NVARCHAR(5) NULL,
    Amount                  DECIMAL(18,2) NULL,
    Reason                  NVARCHAR(MAX) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_WEB_AuxiliaryApplications PRIMARY KEY (Id),
    CONSTRAINT UK_WEB_AuxiliaryApplications_PublicId UNIQUE (PublicId)
);
GO

-- 263. WEB_AffiliationApplications (original: web_solafi)
CREATE TABLE WEB_AffiliationApplications (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    SequenceNumber          INT NULL,
    ApplicationDate         DATE NULL,
    IpAddress               NVARCHAR(50) NULL,
    IdentificationNumber    NVARCHAR(20) NULL,
    IdentificationType      NVARCHAR(5) NULL,
    FirstName               NVARCHAR(150) NULL,
    LastName                NVARCHAR(150) NULL,
    Address                 NVARCHAR(250) NULL,
    Phone                   NVARCHAR(30) NULL,
    Email                   NVARCHAR(200) NULL,
    EmployerName            NVARCHAR(100) NULL,
    Salary                  DECIMAL(18,2) NULL,
    SpouseFirstName         NVARCHAR(150) NULL,
    SpouseLastName          NVARCHAR(150) NULL,
    SpouseIdentificationNumber NVARCHAR(20) NULL,
    SpouseIdentificationType NVARCHAR(5) NULL,
    SpousePhone             NVARCHAR(30) NULL,
    SpouseEmail             NVARCHAR(200) NULL,
    SpouseEmployerName      NVARCHAR(100) NULL,
    SpouseSalary            DECIMAL(18,2) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_WEB_AffiliationApplications PRIMARY KEY (Id),
    CONSTRAINT UK_WEB_AffiliationApplications_PublicId UNIQUE (PublicId)
);
GO

-- 264. WEB_Services (original: web_maeser)
CREATE TABLE WEB_Services (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    ServiceNumber           INT NULL,
    ServiceType             NVARCHAR(2) NULL,
    IdentificationNumber    NVARCHAR(20) NULL,
    CallDate                DATE NULL,
    AppointmentDate         DATE NULL,
    Observations            NVARCHAR(MAX) NULL,
    IsReviewed              BIT NULL,
    ReviewedBy              NVARCHAR(50) NULL,
    Status                  NVARCHAR(2) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_WEB_Services PRIMARY KEY (Id),
    CONSTRAINT UK_WEB_Services_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_WEB_Services_NumTypeIdent UNIQUE (ServiceNumber, ServiceType, IdentificationNumber)
);
GO

-- 265. WEB_ExtraPayments (original: web_extras)
CREATE TABLE WEB_ExtraPayments (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    SequenceNumber          INT NULL,
    PaymentDate             DATE NULL,
    Amount                  DECIMAL(18,2) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_WEB_ExtraPayments PRIMARY KEY (Id),
    CONSTRAINT UK_WEB_ExtraPayments_PublicId UNIQUE (PublicId)
);
GO

-- 266. WEB_DataUpdates (original: web_actdatos)
CREATE TABLE WEB_DataUpdates (
    Id                      BIGINT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    PersonId                INT NULL,
    SequenceNumber          INT NULL,
    UpdateDate              DATE NULL,
    IpAddress               NVARCHAR(50) NULL,
    NewAddress              NVARCHAR(250) NULL,
    NewPhone                NVARCHAR(30) NULL,
    NewMobilePhone          NVARCHAR(30) NULL,
    NewEmail                NVARCHAR(200) NULL,
    NewCity                 NVARCHAR(100) NULL,
    NewDepartment           NVARCHAR(100) NULL,
    NewEmployerName         NVARCHAR(100) NULL,
    NewEmployerAddress      NVARCHAR(250) NULL,
    NewEmployerPhone        NVARCHAR(30) NULL,
    NewPosition             NVARCHAR(100) NULL,
    NewSalary               DECIMAL(18,2) NULL,
    NewMaritalStatus        NVARCHAR(5) NULL,
    NewEducationLevel       NVARCHAR(5) NULL,
    NewHousingType          NVARCHAR(5) NULL,
    SystemDate              DATETIME2 NULL,
    IsProcessed             BIT NULL,
    ProcessedBy             NVARCHAR(50) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_WEB_DataUpdates PRIMARY KEY (Id),
    CONSTRAINT UK_WEB_DataUpdates_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- MODULE: ADM_ — Administration / Multi-tenancy (3 tables)
-- ============================================================

-- 267. ADM_Tenants (nueva)
CREATE TABLE ADM_Tenants (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    Name                    NVARCHAR(200) NOT NULL,
    SchemaName              NVARCHAR(50) NOT NULL,
    Subdomain               NVARCHAR(100) NULL,
    PlanType                NVARCHAR(50) NOT NULL DEFAULT N'Basic',
    IsActive                BIT NOT NULL DEFAULT 1,
    MaxUsers                INT NOT NULL DEFAULT 10,
    StorageLimitMb          BIGINT NOT NULL DEFAULT 5120,
    DatabaseName            NVARCHAR(100) NULL,
    ContactEmail            NVARCHAR(200) NOT NULL,
    ContactPhone            NVARCHAR(30) NULL,
    ActivatedAt             DATETIME2 NULL,
    SuspendedAt             DATETIME2 NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_ADM_Tenants PRIMARY KEY (Id),
    CONSTRAINT UK_ADM_Tenants_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_ADM_Tenants_SchemaName UNIQUE (SchemaName),
    CONSTRAINT UK_ADM_Tenants_Subdomain UNIQUE (Subdomain)
);
GO

-- 268. ADM_Subscriptions (nueva)
CREATE TABLE ADM_Subscriptions (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    TenantId                INT NOT NULL,
    PlanName                NVARCHAR(100) NOT NULL,
    StartDate               DATE NOT NULL,
    EndDate                 DATE NULL,
    MonthlyPrice            DECIMAL(18,2) NOT NULL,
    Currency                NVARCHAR(5) NOT NULL DEFAULT N'COP',
    Status                  NVARCHAR(20) NOT NULL DEFAULT N'Active',
    LastPaymentDate         DATE NULL,
    NextBillingDate         DATE NULL,
    PaymentMethod           NVARCHAR(50) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_ADM_Subscriptions PRIMARY KEY (Id),
    CONSTRAINT UK_ADM_Subscriptions_PublicId UNIQUE (PublicId)
);
GO

-- 269. ADM_TenantSettings (nueva)
CREATE TABLE ADM_TenantSettings (
    Id                      INT IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    TenantId                INT NOT NULL,
    SettingKey               NVARCHAR(200) NOT NULL,
    SettingValue            NVARCHAR(MAX) NULL,
    ValueType               NVARCHAR(30) NOT NULL DEFAULT N'String',
    Description             NVARCHAR(500) NULL,
    ModulePrefix            NVARCHAR(5) NULL,
    CreatedAt               DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
    UpdatedAt               DATETIME2 NULL,
    UpdatedBy               NVARCHAR(100) NULL,
    CONSTRAINT PK_ADM_TenantSettings PRIMARY KEY (Id),
    CONSTRAINT UK_ADM_TenantSettings_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_ADM_TenantSettings_TenantKey UNIQUE (TenantId, SettingKey)
);
GO

-- ============================================================
-- END OF SCHEMA: 34 CREATE TABLE statements
-- SEC_ (11) + AUD_ (14) + WEB_ (6) + ADM_ (3) = 34
-- ============================================================
