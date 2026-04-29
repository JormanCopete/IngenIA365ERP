-- ============================================================
-- IngenIA365ERP — Database Schema v1.0
-- Module: COR_ (Core/Sistema) — 42 tables
-- Generated from PROPUESTA-REDISENO-BD.md
-- ============================================================
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- 1. COR_Countries (sys_paises)
-- Simple country catalog
-- ============================================================
CREATE TABLE [dbo].[COR_Countries] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [Name]          NVARCHAR(100)    NOT NULL,             -- sys_paises.nombre
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Countries] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Countries_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 2. COR_Departments (nueva — no existe tabla legacy)
-- Departamentos/Estados/Provincias
-- ============================================================
CREATE TABLE [dbo].[COR_Departments] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [CountryId]     INT              NOT NULL,             -- FK → COR_Countries (file 08)
    [Code]          NVARCHAR(10)     NOT NULL,
    [Name]          NVARCHAR(100)    NOT NULL,
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Departments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Departments_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_COR_Departments_CountryId_Code] UNIQUE ([CountryId], [Code])
);
GO

-- ============================================================
-- 3. COR_Cities (sys_ciudad57)
-- Columns: CIUDAD→LegacyCode, NOMBRE_CIUDAD→Name, DPTO→DepartmentId
-- ============================================================
CREATE TABLE [dbo].[COR_Cities] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_ciudad57.CIUDAD (int→string for migration)
    [DepartmentId]  INT              NOT NULL,              -- FK → COR_Departments; legacy: DPTO
    [Name]          NVARCHAR(100)    NOT NULL,              -- sys_ciudad57.NOMBRE_CIUDAD
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Cities] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Cities_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 4. COR_Banks (sys_banco03)
-- Columns: CODIGO_BANCO→LegacyCode, NOMBRE→Name, NOMRES→ShortName, etc.
-- ============================================================
CREATE TABLE [dbo].[COR_Banks] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]            NVARCHAR(10)     NULL,         -- sys_banco03.CODIGO_BANCO
    [Name]                  NVARCHAR(80)     NOT NULL,      -- sys_banco03.NOMBRE
    [ShortName]             NVARCHAR(30)     NULL,          -- sys_banco03.NOMRES
    [AccountCode]           NVARCHAR(20)     NULL,          -- sys_banco03.CODCUENTA
    [VoucherTypeCode]       NVARCHAR(10)     NULL,          -- sys_banco03.NUMCOM
    [TransferCode]          NVARCHAR(20)     NULL,          -- sys_banco03.CODTRAS
    [AccountClass]          NVARCHAR(2)      NULL,          -- sys_banco03.CLASE_CUENTA
    [CheckDigitRequired]    BIT              NOT NULL DEFAULT 0, -- sys_banco03.DIGCHEQ ('Y'/'N')
    [LastCheckNumber]       INT              NULL,          -- sys_banco03.ULTIMO_CHEQUE
    [AccountingAccountCode] NVARCHAR(20)     NULL,          -- sys_banco03.CUENTA_CONTABLE
    [PrintFormat]           NVARCHAR(2)      NULL,          -- sys_banco03.formaimprime
    [Copies]                SMALLINT         NULL,          -- sys_banco03.copia
    [FinancialTaxRate]      DECIMAL(6,3)     NOT NULL DEFAULT 0, -- sys_banco03.Grabamenfinanci
    [FileStructure]         NVARCHAR(4)      NULL,          -- sys_banco03.estructura
    [ChargesCommission]     BIT              NOT NULL DEFAULT 0, -- sys_banco03.cobracomision
    [CommissionAccount]     NVARCHAR(20)     NULL,          -- sys_banco03.cuentacomision
    [CommissionType]        INT              NULL,          -- sys_banco03.formacomision
    [CommissionAmount]      DECIMAL(17,4)    NULL,          -- sys_banco03.valorcomision
    [PromptForPrinter]      BIT              NOT NULL DEFAULT 0, -- sys_banco03.pedirimpresora
    [ControlSequential]     NVARCHAR(2)      NULL,          -- sys_banco03.CONTROL_CONSEC
    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Banks] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Banks_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 5. COR_Branches (sys_agencia)
-- Columns: codigo→LegacyCode, nombre→Name, nomres→ShortName
-- ============================================================
CREATE TABLE [dbo].[COR_Branches] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_agencia.codigo
    [Name]          NVARCHAR(80)     NOT NULL,              -- sys_agencia.nombre
    [ShortName]     NVARCHAR(40)     NULL,                  -- sys_agencia.nomres
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Branches] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Branches_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 6. COR_CostCenters (sys_cencos + nom_cencos + cnt_cencos)
-- Columns: CCOSTO→LegacyCode, NOMBRE→Name, plus payroll config
-- ============================================================
CREATE TABLE [dbo].[COR_CostCenters] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]            NVARCHAR(20)     NULL,         -- sys_cencos.CCOSTO
    [Name]                  NVARCHAR(80)     NOT NULL,      -- sys_cencos.NOMBRE
    [CompanyName]           NVARCHAR(100)    NULL,          -- sys_cencos.NombreEmp
    [CompanyTaxId]          NVARCHAR(20)     NULL,          -- sys_cencos.NitEmp
    [PayrollType]           SMALLINT         NOT NULL DEFAULT 0, -- sys_cencos.TipoNom
    [Period]                SMALLINT         NOT NULL DEFAULT 0, -- sys_cencos.Periodo
    [PayrollPeriodicity]    SMALLINT         NOT NULL DEFAULT 0, -- sys_cencos.Desnom
    [PayrollStatus]         NVARCHAR(30)     NULL,          -- sys_cencos.estnom
    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_CostCenters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_CostCenters_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 7. COR_Sections (sys_seccion56)
-- Columns: codigo→LegacyCode, NOMBRE→Name, Nomres→ShortName
-- ============================================================
CREATE TABLE [dbo].[COR_Sections] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_seccion56.codigo
    [Name]          NVARCHAR(80)     NOT NULL,              -- sys_seccion56.NOMBRE
    [ShortName]     NVARCHAR(40)     NULL,                  -- sys_seccion56.Nomres
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Sections] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Sections_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 8. COR_Professions (sys_profe52)
-- Columns: CODIGO_PROFESION→LegacyCode, NOMBRE→Name, NomRes→ShortName
-- ============================================================
CREATE TABLE [dbo].[COR_Professions] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_profe52.CODIGO_PROFESION
    [Name]          NVARCHAR(80)     NOT NULL,              -- sys_profe52.NOMBRE
    [ShortName]     NVARCHAR(40)     NULL,                  -- sys_profe52.NomRes
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Professions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Professions_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 9. COR_Positions (sys_cargo55)
-- Columns: CODIGO_CARGO→LegacyCode, NOMBRE→Name, NOMRES→ShortName
-- ============================================================
CREATE TABLE [dbo].[COR_Positions] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_cargo55.CODIGO_CARGO
    [Name]          NVARCHAR(80)     NOT NULL,              -- sys_cargo55.NOMBRE
    [ShortName]     NVARCHAR(40)     NULL,                  -- sys_cargo55.NOMRES
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Positions] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Positions_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 10. COR_CulturalActivities (sys_cultura54)
-- Columns: codigo→LegacyCode, nombre→Name, nomres→ShortName, comite→CommitteeId
-- ============================================================
CREATE TABLE [dbo].[COR_CulturalActivities] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_cultura54.codigo
    [Name]          NVARCHAR(80)     NOT NULL,              -- sys_cultura54.nombre
    [ShortName]     NVARCHAR(40)     NULL,                  -- sys_cultura54.nomres
    [CommitteeId]   INT              NULL,                  -- FK → COR_Committees; legacy: comite
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_CulturalActivities] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_CulturalActivities_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 11. COR_Sports (sys_deport53)
-- Columns: codigo→LegacyCode, nombre→Name, nomres→ShortName, comite→CommitteeId
-- ============================================================
CREATE TABLE [dbo].[COR_Sports] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_deport53.codigo
    [Name]          NVARCHAR(80)     NOT NULL,              -- sys_deport53.nombre
    [ShortName]     NVARCHAR(40)     NULL,                  -- sys_deport53.nomres
    [CommitteeId]   INT              NULL,                  -- FK → COR_Committees; legacy: comite
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Sports] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Sports_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 12. COR_WithdrawalReasons (sys_motret)
-- Columns: codigo→LegacyCode, nombre→Name, nomres→ShortName
-- ============================================================
CREATE TABLE [dbo].[COR_WithdrawalReasons] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_motret.codigo
    [Name]          NVARCHAR(80)     NOT NULL,              -- sys_motret.nombre
    [ShortName]     NVARCHAR(40)     NULL,                  -- sys_motret.nomres
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_WithdrawalReasons] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_WithdrawalReasons_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 13. COR_Entities (sys_entidad)
-- Columns: codigo→LegacyCode, nombre→Name, nomres→ShortName
-- ============================================================
CREATE TABLE [dbo].[COR_Entities] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_entidad.codigo
    [Name]          NVARCHAR(80)     NOT NULL,              -- sys_entidad.nombre
    [ShortName]     NVARCHAR(40)     NULL,                  -- sys_entidad.nomres
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Entities] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Entities_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 14. COR_Agreements (sys_convenio)
-- Columns: Convenio→LegacyCode, Nombre→Name, plus banking config
-- ============================================================
CREATE TABLE [dbo].[COR_Agreements] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]            NVARCHAR(10)     NULL,         -- sys_convenio.Convenio
    [Name]                  NVARCHAR(80)     NOT NULL,      -- sys_convenio.Nombre
    [AccountNumber]         NVARCHAR(60)     NULL,          -- sys_convenio.Cuenta
    [EntityCode]            NVARCHAR(10)     NULL,          -- sys_convenio.Entidad
    [Currency]              SMALLINT         NOT NULL DEFAULT 0, -- sys_convenio.Moneda
    -- Account type codes
    [SavingsCode]           NVARCHAR(4)      NULL,          -- sys_convenio.Ahorro
    [CheckingCode]          NVARCHAR(4)      NULL,          -- sys_convenio.Corriente
    [BlockCode]             NVARCHAR(4)      NULL,          -- sys_convenio.Bloqueo
    -- Availability config
    [AvailabilityOption]    SMALLINT         NOT NULL DEFAULT 0, -- sys_convenio.OpcDisp
    [AvailabilityLimit]     DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_convenio.CupoDisp
    [AvailabilityRate]      DECIMAL(10,2)    NOT NULL DEFAULT 0, -- sys_convenio.TasaDisp
    -- ATM config
    [AtmOption]             SMALLINT         NOT NULL DEFAULT 0, -- sys_convenio.OpcCajero
    [AtmLimit]              DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_convenio.CupoCajero
    [AtmRate]               DECIMAL(10,2)    NOT NULL DEFAULT 0, -- sys_convenio.TasaCajero
    [AtmTransactions]       SMALLINT         NOT NULL DEFAULT 0, -- sys_convenio.TraCajero
    -- POS config
    [PosOption]             SMALLINT         NOT NULL DEFAULT 0, -- sys_convenio.OpcPos
    [PosLimit]              DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_convenio.CupoPos
    [PosRate]               DECIMAL(10,2)    NOT NULL DEFAULT 0, -- sys_convenio.TasaPos
    [PosTransactions]       SMALLINT         NOT NULL DEFAULT 0, -- sys_convenio.TraPos
    -- Balances and limits
    [ShowBalances]          SMALLINT         NOT NULL DEFAULT 0, -- sys_convenio.Saldos
    [Bin]                   INT              NOT NULL DEFAULT 0, -- sys_convenio.Bin
    [AvailableLimit]        DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_convenio.TopeDisponible
    [CashLimit]             DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_convenio.TopeCaja
    -- File paths
    [OutputPath]            NVARCHAR(200)    NULL,          -- sys_convenio.DirSalida
    [InputPath]             NVARCHAR(200)    NULL,          -- sys_convenio.DirEntrada
    -- Additional config
    [AverageDays]           INT              NOT NULL DEFAULT 0, -- sys_convenio.Promedio
    [FreeTransactions]      INT              NOT NULL DEFAULT 0, -- sys_convenio.Libres
    [HandlingFee]           INT              NOT NULL DEFAULT 0, -- sys_convenio.VlrManejo
    [AvailableLimit2]       DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_convenio.TopeDisponible1
    [CashLimit2]            DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_convenio.TopeCaja1
    [ServiceType]           INT              NOT NULL DEFAULT 0, -- sys_convenio.tiposervicio
    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Agreements] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Agreements_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 15. COR_Courses (sys_curso)
-- Columns: codigo→LegacyCode, nombre→Name, intensidad→Duration, etc.
-- ============================================================
CREATE TABLE [dbo].[COR_Courses] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]        NVARCHAR(10)     NULL,             -- sys_curso.codigo
    [Name]              NVARCHAR(120)    NOT NULL,          -- sys_curso.nombre
    [ShortName]         NVARCHAR(40)     NULL,              -- sys_curso.nomres
    [Duration]          INT              NOT NULL DEFAULT 0, -- sys_curso.intensidad (hours)
    [TeachingEntityId]  INT              NULL,              -- FK → COR_Entities; legacy: entidad_dicta
    [EducationType]     INT              NOT NULL DEFAULT 0, -- sys_curso.tipoeducacion
    [Percentage]        DECIMAL(7,2)     NOT NULL DEFAULT 0, -- sys_curso.porcentaje
    [Amount]            DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_curso.valor
    [CommitteeId]       INT              NULL,              -- FK → COR_Committees; legacy: comite
    [ActivityProgramId] INT              NULL,              -- FK → COR_ActivityPrograms; legacy: programaAct
    -- Audit
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]         NVARCHAR(100)    NULL,
    [UpdatedAt]         DATETIME2        NULL,
    [UpdatedBy]         NVARCHAR(100)    NULL,
    [IsDeleted]         BIT              NOT NULL DEFAULT 0,
    [DeletedAt]         DATETIME2        NULL,
    [DeletedBy]         NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Courses] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Courses_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 16. COR_Relationships (sys_parent51 + nom_parent)
-- Columns: codigo→LegacyCode, nombre→Name, nomres→ShortName
-- ============================================================
CREATE TABLE [dbo].[COR_Relationships] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_parent51.codigo / nom_parent.codigo
    [Name]          NVARCHAR(80)     NOT NULL,              -- nombre
    [ShortName]     NVARCHAR(40)     NULL,                  -- nomres
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Relationships] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Relationships_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 17. COR_Diseases (sys_enfermedades)
-- Columns: codigo→LegacyCode, nombre→Name
-- ============================================================
CREATE TABLE [dbo].[COR_Diseases] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_enfermedades.codigo (int→string)
    [Name]          NVARCHAR(100)    NOT NULL,              -- sys_enfermedades.nombre
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Diseases] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Diseases_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 18. COR_Committees (cop_comite)
-- Columns: codigo→LegacyCode, nombre→Name, nomres→ShortName, tipoComite→CommitteeType
-- ============================================================
CREATE TABLE [dbo].[COR_Committees] (
    [Id]              INT IDENTITY(1,1) NOT NULL,
    [PublicId]         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]      NVARCHAR(10)     NULL,               -- cop_comite.codigo
    [Name]            NVARCHAR(80)     NOT NULL,            -- cop_comite.nombre
    [ShortName]       NVARCHAR(60)     NULL,                -- cop_comite.nomres
    [CommitteeType]   NVARCHAR(2)      NULL,                -- cop_comite.tipoComite
    -- Audit
    [CreatedAt]       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]       NVARCHAR(100)    NULL,
    [UpdatedAt]       DATETIME2        NULL,
    [UpdatedBy]       NVARCHAR(100)    NULL,
    [IsDeleted]       BIT              NOT NULL DEFAULT 0,
    [DeletedAt]       DATETIME2        NULL,
    [DeletedBy]       NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Committees] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Committees_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 19. COR_EmployerCompanies (cop_empresa13 + nom_empresas)
-- Merged fields from both legacy tables
-- ============================================================
CREATE TABLE [dbo].[COR_EmployerCompanies] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]            NVARCHAR(10)     NULL,         -- cop_empresa13.codigo_empresa
    [LegacyPayrollId]       INT              NULL,         -- nom_empresas.IdEmpresa (for payroll migration)
    [Name]                  NVARCHAR(100)    NOT NULL,      -- cop_empresa13.nombre / nom_empresas.Nombre
    [ShortName]             NVARCHAR(40)     NULL,          -- cop_empresa13.nombre_resum / nom_empresas.NomRes
    [TaxId]                 NVARCHAR(20)     NULL,          -- cop_empresa13.Nit / nom_empresas.Nit
    [PayerName]             NVARCHAR(60)     NULL,          -- cop_empresa13.pagador
    [Address]               NVARCHAR(120)    NULL,          -- cop_empresa13.direccion
    [City]                  NVARCHAR(40)     NULL,          -- cop_empresa13.ciudad
    [Phone]                 NVARCHAR(40)     NULL,          -- cop_empresa13.telefono
    [Fax]                   NVARCHAR(40)     NULL,          -- cop_empresa13.fax
    [Email]                 NVARCHAR(200)    NULL,          -- cop_empresa13.email
    [ExpiryDate]            DATE             NULL,          -- cop_empresa13.FechaVence
    -- Cutoff dates config (cop_empresa13)
    [CutoffDate1]           DATE             NULL,          -- cop_empresa13.fecha_corte1
    [CutoffDay1]            SMALLINT         NOT NULL DEFAULT 0, -- cop_empresa13.dia_corte1
    [CutoffDate2]           DATE             NULL,          -- cop_empresa13.fecha_corte2
    [CutoffDay2]            SMALLINT         NOT NULL DEFAULT 0, -- cop_empresa13.dia_corte2
    [CutoffDate3]           DATE             NULL,          -- cop_empresa13.fecha_corte3
    [CutoffDay3]            SMALLINT         NOT NULL DEFAULT 0, -- cop_empresa13.dia_corte3
    [Term]                  SMALLINT         NOT NULL DEFAULT 0, -- cop_empresa13.plazo
    [PayrollConceptCode]    NVARCHAR(10)     NULL,          -- cop_empresa13.CptoNomina
    [SubmissionFormat]      NVARCHAR(30)     NULL,          -- cop_empresa13.FormatoEnvio
    [DiscountPercentage]    DECIMAL(10,3)    NOT NULL DEFAULT 0, -- cop_empresa13.PorDescuento
    [DiscountType]          NVARCHAR(2)      NULL,          -- cop_empresa13.tipodsto
    [IsBlocked]             BIT              NOT NULL DEFAULT 0, -- cop_empresa13.bloquearEmp
    [GroceryPercentage]     DECIMAL(6,3)     NOT NULL DEFAULT 0, -- cop_empresa13.porviveres
    -- Payroll config from nom_empresas
    [ArpRate]               DECIMAL(6,3)     NOT NULL DEFAULT 0, -- nom_empresas.tasaarp
    [AccountType]           INT              NOT NULL DEFAULT 0, -- nom_empresas.TipoCuenta
    [CompanyType]           INT              NOT NULL DEFAULT 0, -- nom_empresas.TipoEmpresa
    [FundSourceAccount]     NVARCHAR(60)     NULL,          -- nom_empresas.CtaOrgFondos
    [FixedProvisionAmount]  DECIMAL(12,2)    NOT NULL DEFAULT 0, -- nom_empresas.ValFijoProv
    [MinimumWageAmount]     DECIMAL(12,2)    NOT NULL DEFAULT 0, -- nom_empresas.ValSalMinimo
    -- Payroll concept IDs (nom_empresas)
    [BasicSalaryConceptId]  INT              NULL,          -- nom_empresas.IdSalBasico
    [TransportConceptId]    INT              NULL,          -- nom_empresas.IdTrasporte
    [SeveranceConceptId]    INT              NULL,          -- nom_empresas.IdCesantias
    [SeveranceInterestId]   INT              NULL,          -- nom_empresas.IdIntCesantias
    [ServiceBonusId]        INT              NULL,          -- nom_empresas.IdPrimaServ
    [VacationConceptId]     INT              NULL,          -- nom_empresas.IdVacasiones
    [IntegralSalaryId]      INT              NULL,          -- nom_empresas.IdSalIntegral
    [SolidarityFundId]      INT              NULL,          -- nom_empresas.IdFdoSolid
    [IndemnityConceptId]    INT              NULL,          -- nom_empresas.IdIndem
    [SocialContrib1Id]      INT              NULL,          -- nom_empresas.IdApoSoc
    [SocialContrib2Id]      INT              NULL,          -- nom_empresas.IdApoSoc1
    [SocialContrib3Id]      INT              NULL,          -- nom_empresas.IdApoSoc2
    [ArpAdminConceptId]     INT              NULL,          -- nom_empresas.IdAdmArp
    [PriorSeveranceId]      INT              NULL,          -- nom_empresas.IdAntCesan
    [WithholdingTaxId]      INT              NULL,          -- nom_empresas.IdRetfte
    [AdminLiquidationType]  INT              NOT NULL DEFAULT 0, -- nom_empresas.ForLiqAdmon
    [SenaConceptId]         INT              NULL,          -- nom_empresas.IdSena
    [IcbfConceptId]         INT              NULL,          -- nom_empresas.IdIcbf
    [NightSurchargeId]      INT              NULL,          -- nom_empresas.IdRecNotur
    [VacationAbsenceId]     INT              NULL,          -- nom_empresas.IdAusVaca
    [ServiceBonus1Id]       INT              NULL,          -- nom_empresas.IdPrimaServ1
    [ServiceBonus2Id]       INT              NULL,          -- nom_empresas.IdPrimaServ2
    [ServiceBonus3Id]       INT              NULL,          -- nom_empresas.IdPrimaServ3
    [ConsolidatedVacId]     INT              NULL,          -- nom_empresas.IdVacaConsol
    [TransportDeductionId]  INT              NULL,          -- nom_empresas.IdDescTrasp
    [MaxDeductionPct]       DECIMAL(6,2)     NOT NULL DEFAULT 0, -- nom_empresas.IdMaxDed
    [IncludeProvision]      INT              NOT NULL DEFAULT 0, -- nom_empresas.IncluProv
    [EmitsInvoice]          BIT              NOT NULL DEFAULT 0, -- nom_empresas.EmiteFact
    [AnnualCompId]          INT              NULL,          -- nom_empresas.IdCompAnual
    [SemiannualCompId]      INT              NULL,          -- nom_empresas.IdCompSem
    [DiscountCompId]        INT              NULL,          -- nom_empresas.IdCompDesc
    [ThirdPartyTransfer]    INT              NOT NULL DEFAULT 0, -- nom_empresas.TrasTer
    [AccountingVoucherId]   NVARCHAR(10)     NULL,          -- nom_empresas.IdCpteCont
    [OffsettingAccount]     NVARCHAR(20)     NULL,          -- nom_empresas.IdCntrprtida
    [AccountingUpdateType]  INT              NOT NULL DEFAULT 0, -- nom_empresas.ForActCont
    [BaseSalary]            DECIMAL(17,2)    NOT NULL DEFAULT 0, -- nom_empresas.SalarioBase
    -- Solidarity fund brackets (nom_empresas)
    [SolidarityBracket1]    DECIMAL(10,2)    NOT NULL DEFAULT 0, -- nom_empresas.fdosolmay1
    [SolidarityBracket2]    DECIMAL(10,2)    NOT NULL DEFAULT 0, -- nom_empresas.fdosolmay2
    [SolidarityBracket3]    DECIMAL(10,2)    NOT NULL DEFAULT 0, -- nom_empresas.fdosolmay3
    [SolidarityBracket4]    DECIMAL(10,2)    NOT NULL DEFAULT 0, -- nom_empresas.fdosolmay4
    [SolidarityBracket5]    DECIMAL(10,2)    NOT NULL DEFAULT 0, -- nom_empresas.fdosolmay5
    [SolidarityRate1]       DECIMAL(6,3)     NOT NULL DEFAULT 0, -- nom_empresas.fdotasamay1
    [SolidarityRate2]       DECIMAL(6,3)     NOT NULL DEFAULT 0, -- nom_empresas.fdotasamay2
    [SolidarityRate3]       DECIMAL(6,3)     NOT NULL DEFAULT 0, -- nom_empresas.fdotasamay3
    [SolidarityRate4]       DECIMAL(6,3)     NOT NULL DEFAULT 0, -- nom_empresas.fdotasamay4
    [SolidarityRate5]       DECIMAL(6,3)     NOT NULL DEFAULT 0, -- nom_empresas.fdotasamay5
    -- Additional payroll config (nom_empresas)
    [SenaApprenticeId]      INT              NULL,          -- nom_empresas.IdAprSena
    [MaxDaysCap]            INT              NOT NULL DEFAULT 0, -- nom_empresas.diastope
    [DisabilityCxcConcept]  INT              NOT NULL DEFAULT 0, -- nom_empresas.cptoincapcxc
    [ProbationDays]         INT              NOT NULL DEFAULT 0, -- nom_empresas.periodoprueba
    [ConsolidatedVacId2]    INT              NOT NULL DEFAULT 0, -- nom_empresas.idvacconsol
    [DisabilityFactor]      DECIMAL(12,2)    NOT NULL DEFAULT 0, -- nom_empresas.factorincap
    [UvtValue]              DECIMAL(17,2)    NOT NULL DEFAULT 0, -- nom_empresas.vlruvt
    [IncomePercentage]      DECIMAL(17,4)    NOT NULL DEFAULT 0, -- nom_empresas.porcrenta
    [DeductionPercentage]   DECIMAL(17,4)    NOT NULL DEFAULT 0, -- nom_empresas.porcdeduc
    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_EmployerCompanies] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_EmployerCompanies_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 20. COR_Advisors (cop_asesores)
-- Columns: Idcedula→LegacyCode, nombre→Name, etc.
-- ============================================================
CREATE TABLE [dbo].[COR_Advisors] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(20)     NULL,                 -- cop_asesores.Idcedula
    [Name]          NVARCHAR(100)    NOT NULL,              -- cop_asesores.nombre
    [Address]       NVARCHAR(120)    NULL,                  -- cop_asesores.direccion
    [Phone]         NVARCHAR(40)     NULL,                  -- cop_asesores.telefono
    [City]          NVARCHAR(20)     NULL,                  -- cop_asesores.ciudad
    [Mobile]        NVARCHAR(30)     NULL,                  -- cop_asesores.movil
    [Email]         NVARCHAR(120)    NULL,                  -- cop_asesores.email
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Advisors] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Advisors_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 21. COR_ActivityPrograms (cop_progactividad)
-- Columns: codigo→LegacyCode, nombre→Name, nomres→ShortName
-- ============================================================
CREATE TABLE [dbo].[COR_ActivityPrograms] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- cop_progactividad.codigo
    [Name]          NVARCHAR(80)     NOT NULL,              -- cop_progactividad.nombre
    [ShortName]     NVARCHAR(60)     NULL,                  -- cop_progactividad.nomres
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_ActivityPrograms] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_ActivityPrograms_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 22. COR_RecreationalEvents (sys_recreacion)
-- Columns: codigo→LegacyCode, detalle→Description, etc.
-- ============================================================
CREATE TABLE [dbo].[COR_RecreationalEvents] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]        NVARCHAR(20)     NULL,             -- sys_recreacion.codigo
    [Description]       NVARCHAR(200)    NOT NULL,          -- sys_recreacion.detalle
    [ActivityType]      NVARCHAR(2)      NULL,              -- sys_recreacion.TipoActividad
    [StartDate]         DATETIME2        NULL,              -- sys_recreacion.fecha_Inicio
    [EndDate]           DATETIME2        NULL,              -- sys_recreacion.fecha_termino
    [Percentage]        DECIMAL(7,2)     NOT NULL DEFAULT 0, -- sys_recreacion.porcentaje
    [Amount]            DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_recreacion.valor
    [CommitteeId]       INT              NULL,              -- FK → COR_Committees; legacy: comite
    [ActivitySubtype]   NVARCHAR(60)     NULL,              -- sys_recreacion.tipoActi_Recre
    [Capacity]          INT              NULL,              -- sys_recreacion.cantcupos
    [ControlNovelty]    BIT              NOT NULL DEFAULT 0, -- sys_recreacion.ctrlnovedad
    [ActivityProgramId] INT              NULL,              -- FK → COR_ActivityPrograms; legacy: programaAct
    -- Audit
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]         NVARCHAR(100)    NULL,
    [UpdatedAt]         DATETIME2        NULL,
    [UpdatedBy]         NVARCHAR(100)    NULL,
    [IsDeleted]         BIT              NOT NULL DEFAULT 0,
    [DeletedAt]         DATETIME2        NULL,
    [DeletedBy]         NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_RecreationalEvents] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_RecreationalEvents_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 23. COR_Sequences (sys_consecu)
-- Columns: CODIGO→LegacyCode, NOMBRE→Name, DOCUMENTO→DocumentType, etc.
-- ============================================================
CREATE TABLE [dbo].[COR_Sequences] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]        NVARCHAR(10)     NULL,             -- sys_consecu.CODIGO
    [Name]              NVARCHAR(80)     NOT NULL,          -- sys_consecu.NOMBRE
    [ShortName]         NVARCHAR(30)     NULL,              -- sys_consecu.NOMRES
    [DocumentType]      NVARCHAR(10)     NULL,              -- sys_consecu.DOCUMENTO
    [IsAutomatic]       BIT              NOT NULL DEFAULT 0, -- sys_consecu.CONSECU_AUTOMA ('Y'/'N')
    [NextSequence]      BIGINT           NOT NULL DEFAULT 1, -- sys_consecu.CONSECU_SIGUIENTE
    -- Audit
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]         NVARCHAR(100)    NULL,
    [UpdatedAt]         DATETIME2        NULL,
    [UpdatedBy]         NVARCHAR(100)    NULL,
    [IsDeleted]         BIT              NOT NULL DEFAULT 0,
    [DeletedAt]         DATETIME2        NULL,
    [DeletedBy]         NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Sequences] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Sequences_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 24. COR_ListParameters (sys_parlistas)
-- Columns: codigo→LegacyCode, descripcion→Description, tipo_lista→ListType
-- ============================================================
CREATE TABLE [dbo].[COR_ListParameters] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]            NVARCHAR(10)     NULL,         -- sys_parlistas.codigo (int→string)
    [Description]           NVARCHAR(120)    NOT NULL,      -- sys_parlistas.descripcion
    [ListType]              NVARCHAR(4)      NULL,          -- sys_parlistas.tipo_lista
    [ValidateExpiryDate]    BIT              NOT NULL DEFAULT 0, -- sys_parlistas.Vali_fech_venci
    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_ListParameters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_ListParameters_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 25. COR_InvoiceParameters (sys_parfactura)
-- Columns: idcodigo→LegacyCode, barcode config fields
-- ============================================================
CREATE TABLE [dbo].[COR_InvoiceParameters] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]        NVARCHAR(10)     NULL,             -- sys_parfactura.idcodigo
    [Tag415]            NVARCHAR(20)     NULL,              -- sys_parfactura.etq415
    [Tag8020]           NVARCHAR(4)      NULL,              -- sys_parfactura.etq8020
    [Tag3900]           NVARCHAR(4)      NULL,              -- sys_parfactura.etq3900
    [Tag96]             NVARCHAR(4)      NULL,              -- sys_parfactura.etq96
    [BarcodeType]       NVARCHAR(4)      NULL,              -- sys_parfactura.TipCodBarra
    [InvoicePrintParam] NVARCHAR(2)      NULL,              -- sys_parfactura.paramimpfact
    [InvoiceGroup]      NVARCHAR(4)      NULL,              -- sys_parfactura.grupofactura
    -- Audit
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]         NVARCHAR(100)    NULL,
    [UpdatedAt]         DATETIME2        NULL,
    [UpdatedBy]         NVARCHAR(100)    NULL,
    [IsDeleted]         BIT              NOT NULL DEFAULT 0,
    [DeletedAt]         DATETIME2        NULL,
    [DeletedBy]         NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_InvoiceParameters] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_InvoiceParameters_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 26. COR_PensionSeveranceParams (sys_parpences)
-- Columns: codigo→LegacyCode, nombre→Name, nomres→ShortName
-- ============================================================
CREATE TABLE [dbo].[COR_PensionSeveranceParams] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_parpences.codigo
    [Name]          NVARCHAR(80)     NOT NULL,              -- sys_parpences.nombre
    [ShortName]     NVARCHAR(40)     NULL,                  -- sys_parpences.nomres
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_PensionSeveranceParams] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_PensionSeveranceParams_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 27. COR_LegalAdvisors (sys_asejuri)
-- Columns: codigo→LegacyCode, nombre→Name, etc.
-- ============================================================
CREATE TABLE [dbo].[COR_LegalAdvisors] (
    [Id]            INT IDENTITY(1,1) NOT NULL,
    [PublicId]       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]    NVARCHAR(10)     NULL,                 -- sys_asejuri.codigo
    [Name]          NVARCHAR(80)     NOT NULL,              -- sys_asejuri.nombre
    [ShortName]     NVARCHAR(40)     NULL,                  -- sys_asejuri.nomres
    [Address]       NVARCHAR(120)    NULL,                  -- sys_asejuri.direccion
    [Phone]         NVARCHAR(40)     NULL,                  -- sys_asejuri.telefono
    [TaxId]         NVARCHAR(30)     NULL,                  -- sys_asejuri.nit
    -- Audit
    [CreatedAt]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]     NVARCHAR(100)    NULL,
    [UpdatedAt]     DATETIME2        NULL,
    [UpdatedBy]     NVARCHAR(100)    NULL,
    [IsDeleted]     BIT              NOT NULL DEFAULT 0,
    [DeletedAt]     DATETIME2        NULL,
    [DeletedBy]     NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_LegalAdvisors] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_LegalAdvisors_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 28. COR_PaymentMethods (sys_forpago)
-- Composite PK converted to surrogate INT Id
-- ============================================================
CREATE TABLE [dbo].[COR_PaymentMethods] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    -- Legacy composite key fields
    [VoucherTypeCode]       NVARCHAR(10)     NOT NULL,      -- sys_forpago.compronte
    [DocumentNumber]        BIGINT           NOT NULL,       -- sys_forpago.numero_domto
    -- Payment breakdown
    [Cash]                  DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_forpago.efectivo
    [Check]                 DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_forpago.cheque
    [BankCode]              NVARCHAR(10)     NULL,           -- sys_forpago.banco
    [CheckNumber]           NVARCHAR(30)     NULL,           -- sys_forpago.nro_cheque
    [AccountNumber]         NVARCHAR(30)     NULL,           -- sys_forpago.nro_cuenta
    [DebitCard]             DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_forpago.tdebito
    [DebitCardNumber]       NVARCHAR(30)     NULL,           -- sys_forpago.nro_tdebito
    [CreditCard]            DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_forpago.tcredito
    [CreditCardNumber]      NVARCHAR(30)     NULL,           -- sys_forpago.nro_tcredito
    [OtherPayment]          DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_forpago.otros
    [OtherPaymentNumber]    NVARCHAR(30)     NULL,           -- sys_forpago.nro_otros
    [TitleAmount]           INT              NOT NULL DEFAULT 0, -- sys_forpago.Vlrtitulo
    [TitleNumber]           NVARCHAR(60)     NULL,           -- sys_forpago.Nrotitulo
    [PaymentReason]         NVARCHAR(4)      NULL,           -- sys_forpago.MotivoPago
    [CashReceiptCount]      INT              NOT NULL DEFAULT 0, -- sys_forpago.regefectivo
    [CheckReceiptCount]     INT              NOT NULL DEFAULT 0, -- sys_forpago.regcheque
    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_PaymentMethods] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_PaymentMethods_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_COR_PaymentMethods_Voucher_Doc] UNIQUE ([VoucherTypeCode], [DocumentNumber])
);
GO

-- ============================================================
-- 29. COR_PaymentMethodChecks (sys_forpago_cheq)
-- Columns: compronte→VoucherTypeCode, numero_domto→DocumentNumber, etc.
-- ============================================================
CREATE TABLE [dbo].[COR_PaymentMethodChecks] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [VoucherTypeCode]   NVARCHAR(10)     NOT NULL,          -- sys_forpago_cheq.compronte
    [DocumentNumber]    BIGINT           NOT NULL,           -- sys_forpago_cheq.numero_domto
    [Amount]            DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_forpago_cheq.cheque
    [CheckNumber]       NVARCHAR(30)     NOT NULL,           -- sys_forpago_cheq.nro_cheque
    [BankCode]          NVARCHAR(10)     NOT NULL,           -- sys_forpago_cheq.banco
    [AccountNumber]     NVARCHAR(30)     NULL,               -- sys_forpago_cheq.nro_cuenta
    [LegacyUser]        NVARCHAR(30)     NULL,               -- sys_forpago_cheq.usuario
    -- Audit
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]         NVARCHAR(100)    NULL,
    [UpdatedAt]         DATETIME2        NULL,
    [UpdatedBy]         NVARCHAR(100)    NULL,
    [IsDeleted]         BIT              NOT NULL DEFAULT 0,
    [DeletedAt]         DATETIME2        NULL,
    [DeletedBy]         NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_PaymentMethodChecks] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_PaymentMethodChecks_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_COR_PaymentMethodChecks_Voucher_Doc_Check_Bank] UNIQUE ([VoucherTypeCode], [DocumentNumber], [CheckNumber], [BankCode])
);
GO

-- ============================================================
-- 30. COR_People (sys_maenit + cnt_nit)
-- THE CENTRAL TABLE — ~80 columns covering identification, contact,
-- demographic, tax, and role flags
-- ============================================================
CREATE TABLE [dbo].[COR_People] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]            NVARCHAR(20)     NULL,         -- sys_maenit.CODIGOTER

    -- === IDENTIFICATION ===
    [LastName]              NVARCHAR(150)    NOT NULL,      -- sys_maenit.APELLIDO
    [FirstName]             NVARCHAR(150)    NOT NULL,      -- sys_maenit.NOMBRE
    [TaxId]                 NVARCHAR(20)     NOT NULL,      -- sys_maenit.NIT
    [TaxIdCheckDigit]       NVARCHAR(2)      NULL,          -- sys_maenit.NIT_CHEQUEO
    [IdIssuedAt]            NVARCHAR(40)     NULL,          -- sys_maenit.EXPEDIDA
    [IdType]                NVARCHAR(2)      NOT NULL,      -- sys_maenit.TIPO_NIT (C=Cedula, N=NIT, E=Ext, U=Unico)
    [IdIssueDate]           DATE             NULL,          -- sys_maenit.FecExpedicion
    [PersonType]            NVARCHAR(2)      NULL,          -- cnt_nit.tipo_persona (N=Natural, J=Juridica)
    [BusinessName]          NVARCHAR(150)    NULL,          -- cnt_nit.RAZON_SOCIAL
    [PreviousCode]          NVARCHAR(20)     NULL,          -- sys_maenit.codigo_ante

    -- === CONTACT ===
    [Address]               NVARCHAR(120)    NULL,          -- sys_maenit.DIRECCION
    [Phone1]                NVARCHAR(40)     NULL,          -- sys_maenit.TELEFONO1
    [Phone2]                NVARCHAR(40)     NULL,          -- sys_maenit.TELEFONO2
    [Fax]                   NVARCHAR(30)     NULL,          -- sys_maenit.FAX
    [Mobile]                NVARCHAR(30)     NULL,          -- sys_maenit.MOVIL
    [Email]                 NVARCHAR(120)    NULL,          -- sys_maenit.EMAIL
    [CityId]                INT              NULL,          -- FK → COR_Cities; legacy: DPTO_CIUDAD
    [MailingAddress]        NVARCHAR(120)    NULL,          -- sys_maenit.DIRECCION_ENVIO
    [MailingPreference]     NVARCHAR(2)      NULL,          -- sys_maenit.ENVIO_DIR
    [MailingCityId]         INT              NULL,          -- FK → COR_Cities; legacy: CIUDAD_ENVIO
    [EmailType]             NVARCHAR(2)      NULL,          -- sys_maenit.Tipo_Correo
    [DaneCityCode]          NVARCHAR(20)     NULL,          -- sys_maenit.CODCIU

    -- === DEMOGRAPHICS ===
    [Gender]                NVARCHAR(2)      NULL,          -- sys_maenit.SEXO
    [MaritalStatus]         NVARCHAR(2)      NULL,          -- sys_maenit.ESTADO_CIVIL
    [DateOfBirth]           DATE             NULL,          -- sys_maenit.FECNACEM
    [EducationLevel]        NVARCHAR(2)      NULL,          -- sys_maenit.NIV_ACADE
    [SocialStratum]         NVARCHAR(4)      NULL,          -- sys_maenit.ESTRATO
    [HousingType]           NVARCHAR(2)      NULL,          -- sys_maenit.TIPO_VIVIENDA
    [HasVehicle]            BIT              NOT NULL DEFAULT 0, -- sys_maenit.VEHICULO ('S'/'N')
    [VehicleType]           INT              NOT NULL DEFAULT 0, -- sys_maenit.tipovehiculo
    [IsHeadOfHousehold]     BIT              NOT NULL DEFAULT 0, -- sys_maenit.CabezaFamilia
    [WorkShift]             NVARCHAR(2)      NULL,          -- sys_maenit.JornadaLaboral
    [NaturalLegalType]      SMALLINT         NOT NULL DEFAULT 0, -- sys_maenit.NATJUR

    -- === EMPLOYMENT ===
    [Employer]              NVARCHAR(80)     NULL,          -- sys_maenit.EMPRESA_LABORA
    [EmployerStartDate]     DATE             NULL,          -- sys_maenit.FEING_EMPRESA
    [SalaryType]            NVARCHAR(2)      NULL,          -- sys_maenit.TIPO_SALARIO
    [Salary]                DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_maenit.SALARIO
    [Severance]             DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_maenit.CESANTIAS
    [ProfessionId]          INT              NULL,          -- FK → COR_Professions; legacy: PROFESION
    [PositionId]            INT              NULL,          -- FK → COR_Positions; legacy: CARGO
    [SeveranceFund]         NVARCHAR(100)    NULL,          -- sys_maenit.FondoCesantia

    -- === TAX & REGULATORY (from cnt_nit) ===
    [WithholdingExempt]     BIT              NOT NULL DEFAULT 0, -- cnt_nit.AUTORFTE
    [IcaWithholdingExempt]  BIT              NOT NULL DEFAULT 0, -- cnt_nit.AUTORTEICA
    [TaxRegime]             NVARCHAR(2)      NULL,          -- cnt_nit.REGIMEN
    [IcaType]               NVARCHAR(6)      NULL,          -- cnt_nit.TIPO_ICA
    [IsLargeContributor]    BIT              NOT NULL DEFAULT 0, -- cnt_nit.gran_contribu
    [IcaRate]               DECIMAL(10,5)    NULL,          -- cnt_nit.TASA_ICA
    [DataOrigin]            NVARCHAR(6)      NULL,          -- cnt_nit.ORIGEN_DATOS
    [PaymentDays]           SMALLINT         NOT NULL DEFAULT 0, -- cnt_nit.DIAS_PAGO
    [HasTaxLien]            BIT              NOT NULL DEFAULT 0, -- cnt_nit.Grabamen
    [HasSpecialPrice]       BIT              NOT NULL DEFAULT 0, -- cnt_nit.PrecioEsp
    [IsEmployerClient]      BIT              NOT NULL DEFAULT 0, -- cnt_nit.CliPatronal
    [SourceWithholding]     BIT              NOT NULL DEFAULT 0, -- cnt_nit.retenFuente
    [NaturalHasRut]         BIT              NOT NULL DEFAULT 0, -- cnt_nit.NaturalTieneRut
    [CiiuCode]              NVARCHAR(20)     NULL,          -- cnt_nit.cod_ciiu
    [CreditLimit]           DECIMAL(17,2)    NOT NULL DEFAULT 0, -- cnt_nit.cupocredito
    [ThirdPartyType]        NVARCHAR(2)      NULL,          -- cnt_nit.tipo_tercero

    -- === BANKING ===
    [BankAccountNumber]     NVARCHAR(30)     NULL,          -- sys_maenit.CUENTA_BANCO
    [BankId]                INT              NULL,          -- FK → COR_Banks; legacy: CODIGO_BANCO
    [BankAccountType]       NVARCHAR(2)      NULL,          -- sys_maenit.TIPO_CUENTA
    [BankAccountCityId]     INT              NULL,          -- FK → COR_Cities; legacy: ciudad_cuenta
    -- cnt_nit banking
    [NitBankCode]           NVARCHAR(20)     NULL,          -- cnt_nit.codigo_banco
    [NitBankAccountType]    NVARCHAR(2)      NULL,          -- cnt_nit.tipo_cuenta_ban
    [NitBankAccountNumber]  NVARCHAR(30)     NULL,          -- cnt_nit.numero_cuenta_ban
    [NitAdvisorId]          NVARCHAR(20)     NULL,          -- cnt_nit.idasesor

    -- === ROLE FLAGS ===
    [IsAssociate]           BIT              NOT NULL DEFAULT 0, -- derived from sys_maenit existence
    [IsEmployee]            BIT              NOT NULL DEFAULT 0, -- sys_maenit.Empleado ('S'/'N')
    [IsAdvisor]             BIT              NOT NULL DEFAULT 0, -- cnt_nit.ASESOR
    [IsThirdParty]          BIT              NOT NULL DEFAULT 0, -- derived from cnt_nit existence
    [ReceivesInvoice]       BIT              NOT NULL DEFAULT 0, -- sys_maenit.RecibeFactura

    -- === STATUS FLAGS ===
    [Status]                NVARCHAR(2)      NULL,          -- cnt_nit.estado / sys_maenit.ESTADO
    [IsDisabled]            BIT              NOT NULL DEFAULT 0, -- sys_maenit.Incapacitado
    [IsInsolvent]           BIT              NOT NULL DEFAULT 0, -- sys_maenit.InsolvenciaEconomica
    [IsOnVacation]          BIT              NOT NULL DEFAULT 0, -- sys_maenit.Vacaciones
    [IsOnUnpaidLeave]       BIT              NOT NULL DEFAULT 0, -- sys_maenit.LicencianoRemunerada
    [IsPensioner]           BIT              NOT NULL DEFAULT 0, -- sys_maenit.Pensionado
    [IsInsubordinate]       BIT              NOT NULL DEFAULT 0, -- sys_maenit.Insubsistente
    [IsDeceased]            BIT              NOT NULL DEFAULT 0, -- sys_maenit.Fallecido
    [IsFromGovernment]      BIT              NOT NULL DEFAULT 0, -- sys_maenit.VienedeGobernacion
    [IsPublicResourceAdmin] BIT              NOT NULL DEFAULT 0, -- sys_maenit.admrecuspub
    [PensionType]           NVARCHAR(10)     NULL,          -- sys_maenit.tipopension
    [SeveranceType]         NVARCHAR(10)     NULL,          -- sys_maenit.tipocesantia

    -- === ONLINE ACCESS ===
    [InternetPassword]      NVARCHAR(20)     NULL,          -- sys_maenit.CLAVE_INTERNET
    [OnlineConsultation]    BIT              NOT NULL DEFAULT 0, -- sys_maenit.ConsultaEnLinea
    [ConsultationStatus]    NVARCHAR(2)      NULL,          -- sys_maenit.EstatusConsulta
    [AffiliationCode]       NVARCHAR(20)     NULL,          -- sys_maenit.CodAfiliacion
    [ConsultationChargeType] NVARCHAR(2)     NULL,          -- sys_maenit.TipoCobroConsulta
    [ConsultationCreditLine] INT             NOT NULL DEFAULT 0, -- sys_maenit.LincredConsulta
    [UserPassword]          NVARCHAR(40)     NULL,          -- sys_maenit.password

    -- === RISK & COMPLIANCE ===
    [AuthCentralRisk]       BIT              NOT NULL DEFAULT 0, -- sys_maenit.autoricentralriesgo
    [PosCardClass]          NVARCHAR(2)      NULL,          -- sys_maenit.clasecupoPos
    [PosCardLimit]          DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_maenit.valorClaseCupo
    [InsuranceRiskRate]     DECIMAL(9,3)     NOT NULL DEFAULT 0, -- sys_maenit.SeguroRiesgo
    [ZoneTypeId]            INT              NOT NULL DEFAULT 0, -- sys_maenit.IdtipoZona
    [ZoneId]                INT              NOT NULL DEFAULT 0, -- sys_maenit.IdZona
    [IsSiplaExempt]         BIT              NOT NULL DEFAULT 0, -- sys_maenit.exoneradoSipa
    [SiplaExemptDate]       DATETIME2        NULL,          -- sys_maenit.fechaexonerado
    [SiplaUser]             NVARCHAR(20)     NULL,          -- sys_maenit.usuariosipla
    [SinglePromissoryNote]  BIT              NOT NULL DEFAULT 0, -- sys_maenit.pagareunico
    [PledgesContributions]  BIT              NOT NULL DEFAULT 0, -- sys_maenit.pignora_aport
    [InManagement]          BIT              NOT NULL DEFAULT 0, -- sys_maenit.engestion

    -- === ACCOUNTING CENTER FLAGS ===
    [CpAdmin]               BIT              NOT NULL DEFAULT 0, -- sys_maenit.CpAdmon
    [CpContributions]       BIT              NOT NULL DEFAULT 0, -- sys_maenit.CpAptos
    [CpLocal]               BIT              NOT NULL DEFAULT 0, -- sys_maenit.CpLocal
    [CpCommission]          BIT              NOT NULL DEFAULT 0, -- sys_maenit.CpComision
    [ProfitCenter]          NVARCHAR(20)     NULL,          -- sys_maenit.cenutilidad
    [CapacityPayPct]        BIT              NOT NULL DEFAULT 0, -- sys_maenit.cappagoPorcentaje

    -- === OTHER INCOME DESCRIPTION ===
    [OtherIncomeDescription] NVARCHAR(120)   NULL,          -- sys_maenit.DESOTROING

    -- === LEGACY AUDIT (from source) ===
    [LegacyUser]            NVARCHAR(20)     NULL,          -- sys_maenit.USUARIO
    [LegacyUserName]        NVARCHAR(80)     NULL,          -- sys_maenit.nomusu
    [LegacyRecordDate]      DATETIME2        NULL,          -- sys_maenit.FECHA_GRABA
    [LegacySystemDate]      DATETIME2        NULL,          -- sys_maenit.fechasys

    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_People] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_People_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_COR_People_TaxId] UNIQUE ([TaxId])
);
GO

-- ============================================================
-- 31. COR_Associates (extracted from sys_maenit association fields)
-- One-to-one with COR_People for associate-specific data
-- ============================================================
CREATE TABLE [dbo].[COR_Associates] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonId]              INT              NOT NULL,      -- FK → COR_People
    [JoinDate]              DATE             NULL,          -- sys_maenit.FECHA_INGRESO
    [ContributionRate]      DECIMAL(5,2)     NOT NULL DEFAULT 0, -- sys_maenit.TASA_APORTE
    [EmployerCompanyId]     INT              NULL,          -- FK → COR_EmployerCompanies; legacy: EMPRESA / CODIGO_EMPRESA
    [BranchId]              INT              NULL,          -- FK → COR_Branches; legacy: AGENCIA
    [CostCenterId]          INT              NULL,          -- FK → COR_CostCenters; legacy: CENCOSTO
    [SectionId]             INT              NULL,          -- FK → COR_Sections; legacy: SECCION_EMPRESA
    [CutoffDay]             SMALLINT         NOT NULL DEFAULT 0, -- sys_maenit.DIA_CORTE
    [Status]                NVARCHAR(2)      NULL,          -- sys_maenit.ESTADO
    [WithdrawalDate]        DATE             NULL,          -- sys_maenit.FECHA_RETIRO
    [WithdrawalReasonId]    INT              NULL,          -- FK → COR_WithdrawalReasons; legacy: MOTIVO_RETIRO
    [RejoinDate]            DATE             NULL,          -- sys_maenit.FECHA_REINGRESO
    [CategoryRating]        NVARCHAR(2)      NULL,          -- sys_maenit.CALIFI_CATEG
    [AdvisorId]             INT              NULL,          -- FK → COR_Advisors; legacy: ASESOR
    [DeductionPeriod]       NVARCHAR(2)      NULL,          -- sys_maenit.PERIODO_DESTO
    [DeductionType]         NVARCHAR(2)      NULL,          -- sys_maenit.CLASE_DESTO
    [Rank]                  NVARCHAR(4)      NULL,          -- sys_maenit.ESCALAFON
    [ReferredBy]            NVARCHAR(20)     NULL,          -- sys_maenit.REFERIDO
    [IsInLegalCollection]   BIT              NOT NULL DEFAULT 0, -- sys_maenit.COBROJUR
    [ContractNumber]        SMALLINT         NOT NULL DEFAULT 0, -- sys_maenit.CONTRACTO
    [ContractExpiryDate]    DATE             NULL,          -- sys_maenit.VENCONTRACTO
    [CommitteeId]           INT              NULL,          -- FK → COR_Committees; legacy: comite
    [ZoneCode]              NVARCHAR(20)     NULL,          -- sys_maenit.COPZONA
    [ContributionPledged]   DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_maenit.SALDO_APORTE (initial contribution pledge)
    [AssociateClass]        NVARCHAR(4)      NULL,          -- sys_maenit.CLASE
    [PaymentType]           NVARCHAR(4)      NULL,          -- sys_maenit.TIPO_PAGO
    [SectorCode]            NVARCHAR(10)     NULL,          -- sys_maenit.SECTOR
    [LastTransferDate]      DATE             NULL,          -- sys_maenit.FECULT_TRASLADO
    [MailingOption]         SMALLINT         NOT NULL DEFAULT 0, -- sys_maenit.ENVIO
    [ManualRating]          NVARCHAR(2)      NULL,          -- sys_maenit.CALMAN
    [PreviousClass]         NVARCHAR(2)      NULL,          -- sys_maenit.claseAnt
    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Associates] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Associates_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_COR_Associates_PersonId] UNIQUE ([PersonId])
);
GO

-- ============================================================
-- 32. COR_Spouses (extracted from sys_maenit CONY* fields)
-- One-to-one with COR_People for spouse data
-- ============================================================
CREATE TABLE [dbo].[COR_Spouses] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonId]              INT              NOT NULL,      -- FK → COR_People
    [SpouseName]            NVARCHAR(80)     NULL,          -- sys_maenit.CONYUGE
    [SpouseIdNumber]        NVARCHAR(20)     NULL,          -- sys_maenit.CONYCEDU
    [SpouseIdType]          NVARCHAR(2)      NULL,          -- sys_maenit.CONYTIPONIT
    [SpouseIdIssuedAt]      NVARCHAR(80)     NULL,          -- sys_maenit.CONYEXPEDIDA
    [SpouseIdIssueDate]     DATE             NULL,          -- sys_maenit.CONYFECEXPEDICION
    [SpouseAddress]         NVARCHAR(80)     NULL,          -- sys_maenit.CONYDIREC
    [SpouseEmployer]        NVARCHAR(80)     NULL,          -- sys_maenit.CONYEMPR
    [SpouseEmployerAddress] NVARCHAR(80)     NULL,          -- sys_maenit.CONYDIREM
    [SpousePhone]           NVARCHAR(30)     NULL,          -- sys_maenit.CONYTELEF
    [SpouseCity]            NVARCHAR(40)     NULL,          -- sys_maenit.CONYCIUD
    [SpouseProfession]      NVARCHAR(10)     NULL,          -- sys_maenit.CONYPROFE
    [SpousePosition]        NVARCHAR(60)     NULL,          -- sys_maenit.CONYCARGO
    [SpouseSalary]          DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_maenit.CONYSALAR
    [SpouseDateOfBirth]     DATE             NULL,          -- sys_maenit.CONYFECNACEM
    [SpouseGender]          NVARCHAR(2)      NULL,          -- sys_maenit.CONYSEXO
    [SpouseFax]             NVARCHAR(30)     NULL,          -- sys_maenit.CONYFAX
    [SpouseMailingPref]     NVARCHAR(2)      NULL,          -- sys_maenit.CONYENVIODIR
    [SpouseMailingAddress]  NVARCHAR(120)    NULL,          -- sys_maenit.CONYENDIRCOR
    [SpouseCompanyCode]     NVARCHAR(10)     NULL,          -- sys_maenit.CONYEMPRESA
    [SpouseBranchCode]      NVARCHAR(10)     NULL,          -- sys_maenit.CONYAGENCIA
    [SpouseSectionCode]     NVARCHAR(10)     NULL,          -- sys_maenit.CONYSECCION
    [SpouseEmployerStart]   DATE             NULL,          -- sys_maenit.CONYFECINGREEMP
    [SpouseEducationLevel]  NVARCHAR(2)      NULL,          -- sys_maenit.CONYNIVACADE
    [SpouseSalaryType]      NVARCHAR(2)      NULL,          -- sys_maenit.CONYTIPOSALARIO
    [SpouseSeverance]       DECIMAL(17,2)    NULL,          -- sys_maenit.CONYCESANTIAS
    [SpouseOtherIncome]     DECIMAL(17,2)    NULL,          -- sys_maenit.CONYOTROING
    [SpouseOtherIncomeDesc] NVARCHAR(120)    NULL,          -- sys_maenit.CONYDESOTROING
    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Spouses] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Spouses_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_COR_Spouses_PersonId] UNIQUE ([PersonId])
);
GO

-- ============================================================
-- 33. COR_PeopleFinancial (extracted from sys_maenit financial fields)
-- One-to-one with COR_People for financial capacity data
-- ============================================================
CREATE TABLE [dbo].[COR_PeopleFinancial] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonId]              INT              NOT NULL,      -- FK → COR_People
    [DebtCapacity]          DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_maenit.CAPA_DEUDA
    [OtherIncome]           DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_maenit.OTRO_INGRESO
    [TotalAssets]           DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_maenit.ACTIVOS
    [VariableIncome]        DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_maenit.IngVariables
    [RentalIncome]          DECIMAL(17,2)    NULL,          -- sys_maenit.IngArriendos
    [PensionIncome]         DECIMAL(17,2)    NULL,          -- sys_maenit.IngPension
    [ThirdPartyDebts]       DECIMAL(17,2)    NULL,          -- sys_maenit.DeudasTerceros
    [MonthlyFixedExpenses]  DECIMAL(17,2)    NULL,          -- sys_maenit.GASTO_FIJO_MES
    [PersonalExpenses]      DECIMAL(17,2)    NULL,          -- sys_maenit.dstoGastosPerso
    [PensionDeduction]      DECIMAL(17,2)    NULL,          -- sys_maenit.DstoPension
    [CreditScore]           DECIMAL(6,2)     NOT NULL DEFAULT 0, -- sys_maenit.Acierta
    [CreditBureauScore]     DECIMAL(15,3)    NOT NULL DEFAULT 0, -- sys_maenit.ScoreCifin
    [CreditBureauRating]    NVARCHAR(4)      NULL,          -- sys_maenit.califidatacredito
    [ExternalDebtPayment]   DECIMAL(15,3)    NOT NULL DEFAULT 0, -- sys_maenit.cuotadeudaexterna
    [ExternalDebtBalance]   DECIMAL(15,3)    NOT NULL DEFAULT 0, -- sys_maenit.saldodeudaexterna
    [PastDueCreditBureau]   DECIMAL(15,3)    NOT NULL DEFAULT 0, -- sys_maenit.SalMorDatacredito
    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_PeopleFinancial] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_PeopleFinancial_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_COR_PeopleFinancial_PersonId] UNIQUE ([PersonId])
);
GO

-- ============================================================
-- 34. COR_AssociateCategories (extracted from sys_maenit CATEGORIA* fields)
-- One-to-one with COR_People for category rating data
-- ============================================================
CREATE TABLE [dbo].[COR_AssociateCategories] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonId]          INT              NOT NULL,          -- FK → COR_People
    [Category1]         NVARCHAR(2)      NULL,              -- sys_maenit.CATEGORIA1
    [Category2]         NVARCHAR(2)      NULL,              -- sys_maenit.CATEGORIA2
    [Category3]         NVARCHAR(2)      NULL,              -- sys_maenit.CATEGORIA3
    [Category4]         NVARCHAR(2)      NULL,              -- sys_maenit.CATEGORIA4
    [Category5]         NVARCHAR(2)      NULL,              -- sys_maenit.CATEGORIA5
    [DaysCategory]      SMALLINT         NOT NULL DEFAULT 0, -- sys_maenit.DIAS_CATEGORIA
    -- Audit
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]         NVARCHAR(100)    NULL,
    [UpdatedAt]         DATETIME2        NULL,
    [UpdatedBy]         NVARCHAR(100)    NULL,
    [IsDeleted]         BIT              NOT NULL DEFAULT 0,
    [DeletedAt]         DATETIME2        NULL,
    [DeletedBy]         NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_AssociateCategories] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_AssociateCategories_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_COR_AssociateCategories_PersonId] UNIQUE ([PersonId])
);
GO

-- ============================================================
-- 35. COR_CommitteeMembers (cop_comiteasoc)
-- Many-to-many: PersonId (codigoter) ↔ CommitteeId (idComite)
-- ============================================================
CREATE TABLE [dbo].[COR_CommitteeMembers] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonId]          INT              NOT NULL,          -- FK → COR_People; legacy: codigoter
    [CommitteeId]       INT              NOT NULL,          -- FK → COR_Committees; legacy: idComite
    [LegacyUser]        NVARCHAR(100)    NULL,              -- cop_comiteasoc.usuario
    [LegacyDate]        DATETIME2        NULL,              -- cop_comiteasoc.fechaSys
    -- Audit
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]         NVARCHAR(100)    NULL,
    [UpdatedAt]         DATETIME2        NULL,
    [UpdatedBy]         NVARCHAR(100)    NULL,
    [IsDeleted]         BIT              NOT NULL DEFAULT 0,
    [DeletedAt]         DATETIME2        NULL,
    [DeletedBy]         NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_CommitteeMembers] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_CommitteeMembers_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_COR_CommitteeMembers_Person_Committee] UNIQUE ([PersonId], [CommitteeId])
);
GO

-- ============================================================
-- 36. COR_Beneficiaries (cop_benef + nom_bene)
-- Merged: cop_benef for associate beneficiaries, nom_bene for employee beneficiaries
-- ============================================================
CREATE TABLE [dbo].[COR_Beneficiaries] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonId]              INT              NOT NULL,      -- FK → COR_People; legacy: cop_benef.Codigoter
    [BeneficiaryIdNumber]   NVARCHAR(20)     NOT NULL,      -- cop_benef.Cedula
    [BeneficiaryName]       NVARCHAR(100)    NOT NULL,      -- cop_benef.Nombre / nom_bene (nombres+apellidos)
    [DocumentType]          NVARCHAR(4)      NULL,          -- cop_benef.TipoDocumento
    [RelationshipId]        INT              NULL,          -- FK → COR_Relationships; legacy: CodParentesco / Idparent
    [DateOfBirth]           DATE             NULL,          -- cop_benef.FechaNacimiento / nom_bene.fecnac
    [EducationLevel]        NVARCHAR(2)      NULL,          -- cop_benef.NivelAcademico
    [HasDisability]         BIT              NOT NULL DEFAULT 0, -- cop_benef.Discapacidad
    [IsEmployed]            BIT              NOT NULL DEFAULT 0, -- cop_benef.Trabaja
    [Percentage]            DECIMAL(6,3)     NOT NULL DEFAULT 0, -- cop_benef.porcentaje
    [Gender]                NVARCHAR(2)      NULL,          -- cop_benef.Sexo
    [Phone]                 NVARCHAR(20)     NULL,          -- cop_benef.Telefono
    [Address]               NVARCHAR(120)    NULL,          -- cop_benef.direccion
    [CityId]                INT              NULL,          -- FK → COR_Cities; legacy: cop_benef.ciudad
    [BeneficiaryType]       NVARCHAR(2)      NOT NULL DEFAULT 'A', -- A=Associate, E=Employee
    [Status]                NVARCHAR(2)      NULL,          -- cop_benef.estado
    [LegacyBenefCode]       NVARCHAR(20)     NULL,          -- cop_benef.codigobenef
    [LegacyNewId]           NVARCHAR(20)     NULL,          -- cop_benef.IdBenefNuevo
    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Beneficiaries] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Beneficiaries_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 37. COR_References (sys_referencia)
-- Personal and commercial references for a person
-- ============================================================
CREATE TABLE [dbo].[COR_References] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [PersonId]          INT              NOT NULL,          -- FK → COR_People; legacy: codigoter
    [ReferenceType]     NVARCHAR(2)      NOT NULL,          -- sys_referencia.TipoReferencia
    [Name]              NVARCHAR(120)    NOT NULL,           -- sys_referencia.nombre
    [Address]           NVARCHAR(120)    NULL,               -- sys_referencia.direccion
    [Phone]             NVARCHAR(40)     NULL,               -- sys_referencia.Telefono
    [CityId]            INT              NULL,               -- FK → COR_Cities; legacy: ciudad
    [ContactName]       NVARCHAR(120)    NULL,               -- sys_referencia.contacto
    [ProductType]       NVARCHAR(2)      NULL,               -- sys_referencia.Tipo_Producto
    [ProductNumber]     INT              NULL,               -- sys_referencia.NroPrducto
    [Mobile]            NVARCHAR(40)     NULL,               -- sys_referencia.celular
    [RelationshipCode]  NVARCHAR(10)     NULL,               -- sys_referencia.parentesco
    -- Audit
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]         NVARCHAR(100)    NULL,
    [UpdatedAt]         DATETIME2        NULL,
    [UpdatedBy]         NVARCHAR(100)    NULL,
    [IsDeleted]         BIT              NOT NULL DEFAULT 0,
    [DeletedAt]         DATETIME2        NULL,
    [DeletedBy]         NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_References] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_References_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 38. COR_Companies (sys_compania)
-- THE company configuration table with ~70 legacy fields
-- organized into logical sections
-- ============================================================
CREATE TABLE [dbo].[COR_Companies] (
    [Id]                    INT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [LegacyCode]            NVARCHAR(10)     NULL,         -- sys_compania.CODIGO

    -- === BASIC INFO ===
    [Name]                  NVARCHAR(120)    NOT NULL,      -- sys_compania.NOMBRE
    [ShortName]             NVARCHAR(60)     NULL,          -- sys_compania.NOMRES
    [TaxId]                 NVARCHAR(20)     NOT NULL,      -- sys_compania.NIT
    [TaxIdCheckDigit]       NVARCHAR(2)      NULL,          -- derived
    [Address]               NVARCHAR(80)     NULL,          -- sys_compania.DIRECCION
    [Phone]                 NVARCHAR(40)     NULL,          -- sys_compania.TELEFONO
    [City]                  NVARCHAR(40)     NULL,          -- sys_compania.CIUDAD
    [Department]            NVARCHAR(40)     NULL,          -- sys_compania.DPTO
    [Activity]              NVARCHAR(10)     NULL,          -- sys_compania.ACTIVIDAD
    [PersonCode]            NVARCHAR(20)     NULL,          -- sys_compania.CODIGOTER (entity's own person code)

    -- === DIAN / INVOICE RESOLUTION ===
    [DianCode]              NVARCHAR(6)      NULL,          -- sys_compania.CODIGO_DIAN
    [DianCodeDescription]   NVARCHAR(40)     NULL,          -- sys_compania.DESC_CODI_DIAN
    [DianResolutionNumber]  NVARCHAR(40)     NULL,          -- sys_compania.NUM_RESOL_DIAN
    [DianResolutionDate]    DATE             NULL,          -- sys_compania.FEC_RESOL_DIAN
    [DianInvoiceStart]      INT              NOT NULL DEFAULT 0, -- sys_compania.NUMERO_INICI_DIAN
    [DianInvoiceEnd]        INT              NOT NULL DEFAULT 0, -- sys_compania.NUMERO_FINAL_DIAN

    -- === WAGE CONFIGURATION ===
    [LegalMinimumWage]      DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_compania.SALARIO_MINI_LEGAL
    [CompanyMinimumWage]    DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_compania.SALARIO_MINI_EMPRESA
    [MinimumWage]           DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_compania.salario_minimo

    -- === PORTFOLIO / COLLECTION CONFIG ===
    [ChargesDefaultInterest] BIT             NOT NULL DEFAULT 0, -- sys_compania.COBRA_INT_MORA
    [LatePaymentControl]    BIT              NOT NULL DEFAULT 0, -- sys_compania.CONTROL_ATRAZO
    [PaymentControl]        BIT              NOT NULL DEFAULT 0, -- sys_compania.CONTROL_ABONOS
    [CollectionPeriod]      NVARCHAR(10)     NULL,          -- sys_compania.PERIODO_COBRO
    [InitialDays]           SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.DIAS_INICIALES
    [FinalDays]             SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.DIAS_FINALES
    [DefaultRate]           DECIMAL(7,4)     NULL,          -- sys_compania.TASA_MORA
    [GraceDays]             SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.DIAS_GRACIA
    [UsuryRate]             DECIMAL(7,4)     NULL,          -- sys_compania.TASA_USURA
    [EffectiveRate]         BIT              NOT NULL DEFAULT 0, -- sys_compania.TASA_EFEC
    [LiquidationBase]       NVARCHAR(2)      NULL,          -- sys_compania.BASE_LIQUIDACION
    [LiquidationType]       NVARCHAR(2)      NULL,          -- sys_compania.TIPO_LIQUIDACION
    [DiscountClass]         NVARCHAR(2)      NULL,          -- sys_compania.CLASE_DCTO
    [LiquidationClass]      NVARCHAR(2)      NULL,          -- sys_compania.CLASE_LIQUIDACION
    [QuotaType]             NVARCHAR(2)      NULL,          -- sys_compania.TIPO_CUPO
    [Quota]                 DECIMAL(5,2)     NOT NULL DEFAULT 0, -- sys_compania.CUPO
    [ReportClass]           NVARCHAR(2)      NULL,          -- sys_compania.CLASE_INFORME
    [FileName]              NVARCHAR(40)     NULL,          -- sys_compania.NOMBRE_ARCHIVO
    [CreditSequenceNum]     BIGINT           NOT NULL DEFAULT 0, -- sys_compania.NUM_SOLCRED
    [CreditSequenceCtrl]    DECIMAL(15,0)    NOT NULL DEFAULT 0, -- sys_compania.ConseCreditos
    [NumCodeudores]         INT              NOT NULL DEFAULT 0, -- sys_compania.numcodecredit

    -- === DUE DATE RANGES ===
    [DueRangeStart01]       SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.VEMTO_INI_01
    [DueRangeEnd01]         SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.VEMTO_FIN_01
    [DueRangeStart02]       SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.VEMTO_INI_02
    [DueRangeEnd02]         SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.VEMTO_FIN_02
    [DueRangeStart03]       SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.VEMTO_INI_03
    [DueRangeEnd03]         SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.VEMTO_FIN_03
    [DueRangeStart04]       SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.VEMTO_INI_04
    [DueRangeEnd04]         SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.VEMTO_FIN_04
    [DueRangeStart05]       SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.VEMTO_INI_05
    [DueRangeEnd05]         SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.VEMTO_FIN_05

    -- === CONCEPT CODES (current) ===
    [ConceptCapital]        NVARCHAR(10)     NULL,          -- sys_compania.CPTO_CAPITAL
    [ConceptInterest]       NVARCHAR(10)     NULL,          -- sys_compania.CPTO_INTERES
    [ConceptAdmin]          NVARCHAR(10)     NULL,          -- sys_compania.CPTO_ADMON
    [ConceptInsurance]      NVARCHAR(10)     NULL,          -- sys_compania.CPTO_SEGURO
    [ConceptContributions]  NVARCHAR(10)     NULL,          -- sys_compania.CPTO_APORTES
    [ConceptSavings]        NVARCHAR(10)     NULL,          -- sys_compania.CPTO_AHORROS
    [ConceptAffiliation]    NVARCHAR(10)     NULL,          -- sys_compania.CPTO_AFILIACION
    [ConceptExtra]          NVARCHAR(10)     NULL,          -- sys_compania.Cpto_extra
    [ConceptImmovable]      NVARCHAR(10)     NULL,          -- sys_compania.CPTO_INMO
    [ConceptService]        NVARCHAR(10)     NULL,          -- sys_compania.CPTO_SERVI
    [ConceptOther1]         NVARCHAR(10)     NULL,          -- sys_compania.CPTO_OTRO1
    [ConceptOther2]         NVARCHAR(10)     NULL,          -- sys_compania.CPTO_OTRO2
    [ConceptRevaluation]    NVARCHAR(10)     NULL,          -- sys_compania.CptoRevapo
    [ConceptContribDisp]    NVARCHAR(10)     NULL,          -- sys_compania.CPTO_DISAPO
    [Concept4Mil]           NVARCHAR(10)     NULL,          -- sys_compania.Cpto4Mil
    [ConceptWithholdingLate] NVARCHAR(10)    NULL,          -- sys_compania.cptoretfteatra
    [ConceptCdtInterest]    NVARCHAR(10)     NULL,          -- sys_compania.cptointcdats
    [ConceptWithholding]    NVARCHAR(10)     NULL,          -- sys_compania.cptoretfte
    [ConceptCdt]            NVARCHAR(10)     NULL,          -- sys_compania.cptocdats
    [ConceptSurplus]        NVARCHAR(10)     NULL,          -- sys_compania.SOBRA

    -- === CONCEPT CODES (late/arrears) ===
    [ConceptCapitalLate]    NVARCHAR(10)     NULL,          -- sys_compania.CPTO_CAPIATRA
    [ConceptInterestLate]   NVARCHAR(10)     NULL,          -- sys_compania.CPTO_INTEATRA
    [ConceptAdminLate]      NVARCHAR(10)     NULL,          -- sys_compania.CPTO_ADMATRA
    [ConceptInsuranceLate]  NVARCHAR(10)     NULL,          -- sys_compania.CPTO_SEGUATR
    [ConceptContribLate]    NVARCHAR(10)     NULL,          -- sys_compania.CPTO_APORATR
    [ConceptSavingsLate]    NVARCHAR(10)     NULL,          -- sys_compania.CPTO_AHORATR
    [ConceptAffiliationLate] NVARCHAR(10)    NULL,          -- sys_compania.CPTO_AFILIATRA

    -- === PRIORITY ORDER ===
    [PriorityCapital]       NVARCHAR(4)      NULL,          -- sys_compania.PRIOR_CAPITAL
    [PriorityInterest]      NVARCHAR(4)      NULL,          -- sys_compania.PRIOR_INTERES
    [PriorityServices]      NVARCHAR(4)      NULL,          -- sys_compania.PRIOR_SERVICIOS
    [PriorityDefault]       NVARCHAR(4)      NULL,          -- sys_compania.PRIOR_MORA
    [PriorityAdmin]         NVARCHAR(4)      NULL,          -- sys_compania.PRIOR_ADMON
    [PriorityInsurance]     NVARCHAR(4)      NULL,          -- sys_compania.PRIOR_SEGURO
    [PriorityContributions] NVARCHAR(4)      NULL,          -- sys_compania.PRIOR_APORTES
    [PrioritySavings]       NVARCHAR(4)      NULL,          -- sys_compania.PRIOR_AHORROS
    [PriorityAffiliation]   NVARCHAR(4)      NULL,          -- sys_compania.PRIOR_AFILIACION

    -- === ANTI-MONEY LAUNDERING ===
    [DailyAmlLimit]         DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_compania.LIM_DIARIO_LAVA_ACTI
    [MonthlyAmlLimit]       DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_compania.LIM_MES_LAVA_ACTI
    [AmlSequence]           BIGINT           NOT NULL DEFAULT 0, -- sys_compania.consec_lavado
    [CausesLegalCollection] BIT              NOT NULL DEFAULT 0, -- sys_compania.CausaCobJur

    -- === ACCOUNTING ACCOUNTS ===
    [AccountingAccount1]    NVARCHAR(20)     NULL,          -- sys_compania.CUENTACON1
    [AccountingAccount2]    NVARCHAR(20)     NULL,          -- sys_compania.CUENTACON2
    [AccountingAccount3]    NVARCHAR(20)     NULL,          -- sys_compania.CUENTACON3
    [AccountingAccount4]    NVARCHAR(20)     NULL,          -- sys_compania.CUENTACON4

    -- === ADJUSTMENT ACCOUNTS ===
    [AdjInterest]           NVARCHAR(20)     NULL,          -- sys_compania.AjuInteres
    [AdjOrderAccounts]      NVARCHAR(20)     NULL,          -- sys_compania.AjuCtasOrden
    [AdjPortfolioProvision] NVARCHAR(20)     NULL,          -- sys_compania.AjuProvCart
    [AdjInterestProvision]  NVARCHAR(20)     NULL,          -- sys_compania.AjuProvInt
    [AdjProvisionPayroll]   NVARCHAR(20)     NULL,          -- sys_compania.ajuProvNom
    [AdjProvisionCash]      NVARCHAR(20)     NULL,          -- sys_compania.ajuProvCaj
    -- Adjustment detail accounts
    [AdjCashContrib]        NVARCHAR(10)     NULL,          -- sys_compania.ajuscaapor
    [AdjCashCapital]        NVARCHAR(10)     NULL,          -- sys_compania.ajuscacapi
    [AdjCashInterest]       NVARCHAR(10)     NULL,          -- sys_compania.ajuscainte
    [AdjCashDefaultInt]     NVARCHAR(10)     NULL,          -- sys_compania.ajuscaintemor
    [AdjCashInsurance]      NVARCHAR(10)     NULL,          -- sys_compania.ajuscasegcre
    [AdjCashService]        NVARCHAR(10)     NULL,          -- sys_compania.ajuscaserv
    [AdjCashSavings]        NVARCHAR(10)     NULL,          -- sys_compania.ajuscaahor
    [AdjCashExtra]          NVARCHAR(10)     NULL,          -- sys_compania.ajuscaext
    [AdjCashAdmin]          NVARCHAR(10)     NULL,          -- sys_compania.ajuscaadm

    -- === SIGNATURES ===
    [RepresentativeName]    NVARCHAR(80)     NULL,          -- sys_compania.REPRE
    [AuditorName]           NVARCHAR(80)     NULL,          -- sys_compania.REV_FIS
    [AuditorLicense]        NVARCHAR(30)     NULL,          -- sys_compania.MAT_REV
    [AccountantName]        NVARCHAR(80)     NULL,          -- sys_compania.CONTA
    [AccountantLicense]     NVARCHAR(30)     NULL,          -- sys_compania.MAT_CON
    [CollectionManager]     NVARCHAR(80)     NULL,          -- sys_compania.JefeCartera
    [OtherSignerName]       NVARCHAR(80)     NULL,          -- sys_compania.nombre_otros
    [OtherSignerPosition]   NVARCHAR(80)     NULL,          -- sys_compania.cargo_otros
    -- Signature images stored as VARBINARY(MAX) instead of legacy IMAGE
    [RepresentativeSign]    VARBINARY(MAX)   NULL,          -- sys_compania.firma_repre
    [AccountantSign]        VARBINARY(MAX)   NULL,          -- sys_compania.firma_conta
    [AuditorSign]           VARBINARY(MAX)   NULL,          -- sys_compania.firma_revi
    [CollectionSign]        VARBINARY(MAX)   NULL,          -- sys_compania.firma_cart
    [OtherSign]             VARBINARY(MAX)   NULL,          -- sys_compania.firma_otros

    -- === AUTO-CREATE FLAGS ===
    [AutoCreateAccount]     BIT              NOT NULL DEFAULT 0, -- sys_compania.CREA_CUENTA
    [AutoCreateTaxId]       BIT              NOT NULL DEFAULT 0, -- sys_compania.CREA_NIT
    [AutoCreateBranch]      BIT              NOT NULL DEFAULT 0, -- sys_compania.CREA_AGENCIA
    [AutoCreateCostCenter]  BIT              NOT NULL DEFAULT 0, -- sys_compania.CREA_CENCOSTO

    -- === SEQUENCE COUNTERS ===
    [DepositSequence]       DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_compania.CONSEDEP
    [CdtSequence]           DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_compania.CONSECDAT
    [SequenceControl]       SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.CtrlConse
    [ControlDepositSeq]     BIT              NOT NULL DEFAULT 0, -- sys_compania.controlaconsedep

    -- === ADVANCE PAYMENT CONFIG ===
    [AdvanceConceptType]    SMALLINT         NOT NULL DEFAULT 0, -- sys_compania.CptoAnticipo
    [AdvanceVoucherCode]    NVARCHAR(10)     NULL,          -- sys_compania.CpteAnticipo
    [FavorVoucherCode]      NVARCHAR(10)     NULL,          -- sys_compania.CpteFavor
    [DebtRecoveryOption]    INT              NOT NULL DEFAULT 0, -- sys_compania.OpRecDeuda

    -- === ONLINE QUERY CONFIG ===
    [GenerateQueryCharge]   BIT              NOT NULL DEFAULT 0, -- sys_compania.GenCobroConsulta
    [QueryChargeAmount]     DECIMAL(17,2)    NOT NULL DEFAULT 0, -- sys_compania.Vlr_Consulta
    [QueryVoucherCode]      NVARCHAR(10)     NULL,          -- sys_compania.cpte_Consulta

    -- === PAYROLL & APPLICATION MODE ===
    [PayrollClass]          NVARCHAR(2)      NULL,          -- sys_compania.CLASE_NOMINA
    [SignatureModule]       BIT              NOT NULL DEFAULT 0, -- sys_compania.MODULO_FIRMAS
    [CalculateBalance]      BIT              NOT NULL DEFAULT 0, -- sys_compania.CALCULA_SALDO
    [PayrollApplicationMode] NVARCHAR(2)     NULL,          -- sys_compania.FORAPL_NOMI
    [CashApplicationMode]   NVARCHAR(2)      NULL,          -- sys_compania.FORAPL_CAJA
    [ChargesCodebtor]       BIT              NOT NULL DEFAULT 0, -- sys_compania.cobracodeudor

    -- === OVERRIDE / RESTRICTION FLAGS ===
    [OverrideExtra]         BIT              NOT NULL DEFAULT 0, -- sys_compania.R_EXTRA
    [OverrideQuota]         BIT              NOT NULL DEFAULT 0, -- sys_compania.R_CUPO
    [OverrideTerm]          BIT              NOT NULL DEFAULT 0, -- sys_compania.R_PLAZO
    [OverrideRate]          BIT              NOT NULL DEFAULT 0, -- sys_compania.R_TASA
    [UnifiedNetwork]        BIT              NOT NULL DEFAULT 0, -- sys_compania.UNI_RED
    [RequiresStudy]         BIT              NOT NULL DEFAULT 0, -- sys_compania.ManEstudio
    [DateRestrictionRC]     BIT              NOT NULL DEFAULT 0, -- sys_compania.restri_fecha_rc
    [AllowCreditQuotaMod]   BIT              NOT NULL DEFAULT 0, -- sys_compania.Modif_Cuota_Cred
    [BankReconciliation]    BIT              NOT NULL DEFAULT 0, -- sys_compania.conciliabanca
    [CalcContribProvision]  BIT              NOT NULL DEFAULT 0, -- sys_compania.calcprovaportes
    [ApplyDefaultSuspension] BIT             NOT NULL DEFAULT 0, -- sys_compania.aplisuspmora
    [DefaultSuspensionDays] INT              NOT NULL DEFAULT 0, -- sys_compania.diassuspmora
    [DefaultForWithdrawn]   BIT              NOT NULL DEFAULT 0, -- sys_compania.mora_retirados
    [InvoiceSequenceCtrl]   BIT              NOT NULL DEFAULT 0, -- sys_compania.consecut_factura
    [AccrualParam]          BIT              NOT NULL DEFAULT 0, -- sys_compania.paramcausacion
    [DisableCdtRate]        BIT              NOT NULL DEFAULT 0, -- sys_compania.DisableTasaICdat

    -- === EMAIL CONFIGURATION ===
    [SmtpServer]            NVARCHAR(100)    NULL,          -- sys_compania.ServerSmtp
    [SmtpSenderEmail]       NVARCHAR(120)    NULL,          -- sys_compania.CorreoEnvio
    [SmtpPassword]          NVARCHAR(100)    NULL,          -- sys_compania.PasswordEnvio (should be encrypted)
    [SmtpPort]              INT              NULL,          -- sys_compania.puertoSMTP
    [SmtpEnableSsl]         BIT              NOT NULL DEFAULT 0, -- sys_compania.EnabledSSL
    [WebServiceUrl]         NVARCHAR(200)    NULL,          -- sys_compania.Webservice

    -- === PROMISSORY NOTE CONFIG ===
    [PromissoryNumber]      DECIMAL(10,0)    NOT NULL DEFAULT 0, -- sys_compania.numpagare
    [PromissoryFormat]      NVARCHAR(2)      NULL,          -- sys_compania.formapagare
    [PromissoryNotes]       BIT              NOT NULL DEFAULT 0, -- sys_compania.pagarenotas

    -- === LEGAL ENTITY ===
    [LegalEntityNumber]     NVARCHAR(20)     NULL,          -- sys_compania.NumPersoneria
    [LegalEntityDate]       DATE             NULL,          -- sys_compania.FecPersoneria

    -- === WEB SYNC ===
    [UploadAssociateWeb]    INT              NULL,          -- sys_compania.UploadAsocWeb
    [UploadMovementWeb]     INT              NULL,          -- sys_compania.UploadMovimtoWeb
    [DownloadWeb]           BIT              NOT NULL DEFAULT 0, -- sys_compania.DownloadWeb

    -- === WITHHOLDING AUX ===
    [WithholdingAux]        BIT              NOT NULL DEFAULT 0, -- sys_compania.retenaux
    [WithholdingAuxAmount]  DECIMAL(17,2)    NULL,          -- sys_compania.valor_retenaux
    [WithholdingAuxPct]     DECIMAL(17,2)    NULL,          -- sys_compania.porcen_retenaux
    [WithholdingAuxAccount] NVARCHAR(20)     NULL,          -- sys_compania.cuenta_retenaux

    -- === DATA PACKAGE / MISC ===
    [DataPackageSize]       INT              NOT NULL DEFAULT 0, -- sys_compania.paquetedatos
    [AgreementType]         INT              NOT NULL DEFAULT 0, -- sys_compania.tipconv
    [FeecCode]              NVARCHAR(10)     NULL,          -- sys_compania.feec
    [LicenseType]           NVARCHAR(4)      NULL,          -- sys_compania.tipolic
    [LicenseExpiryDate]     DATE             NULL,          -- sys_compania.fechavelic
    [RegistrationExpiryDate] DATE            NULL,          -- sys_compania.fechaperreg
    [NoticeDays]            INT              NOT NULL DEFAULT 0, -- sys_compania.diasaviso
    [ReportType]            NVARCHAR(4)      NULL,          -- sys_compania.informe

    -- === LEGACY AUDIT ===
    [LegacyUser]            NVARCHAR(20)     NULL,          -- sys_compania.usuario
    [LegacyUserName]        NVARCHAR(80)     NULL,          -- sys_compania.nomusu
    [LegacySystemDate]      DATETIME2        NULL,          -- sys_compania.fechasys

    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Companies] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Companies_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 39. COR_SystemSettings (nueva — key/value config store)
-- Replaces scattered config in multiple legacy tables
-- ============================================================
CREATE TABLE [dbo].[COR_SystemSettings] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [SettingKey]        NVARCHAR(200)    NOT NULL,
    [SettingValue]      NVARCHAR(MAX)    NULL,
    [ValueType]         NVARCHAR(20)     NOT NULL DEFAULT N'String', -- String, Int, Decimal, Bool, Json
    [Description]       NVARCHAR(500)    NULL,
    [ModulePrefix]      NVARCHAR(10)     NULL,              -- COR, CNT, COP, NOM, INV, etc.
    -- Audit
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]         NVARCHAR(100)    NULL,
    [UpdatedAt]         DATETIME2        NULL,
    [UpdatedBy]         NVARCHAR(100)    NULL,
    [IsDeleted]         BIT              NOT NULL DEFAULT 0,
    [DeletedAt]         DATETIME2        NULL,
    [DeletedBy]         NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_SystemSettings] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_SystemSettings_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_COR_SystemSettings_Key] UNIQUE ([SettingKey])
);
GO

-- ============================================================
-- 40. COR_NotificationTemplates (nueva — email/SMS/push templates)
-- ============================================================
CREATE TABLE [dbo].[COR_NotificationTemplates] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [TemplateName]      NVARCHAR(200)    NOT NULL,
    [Channel]           NVARCHAR(10)     NOT NULL,          -- Email, SMS, Push
    [Subject]           NVARCHAR(500)    NULL,
    [BodyTemplate]      NVARCHAR(MAX)    NOT NULL,
    [IsActive]          BIT              NOT NULL DEFAULT 1,
    -- Audit
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]         NVARCHAR(100)    NULL,
    [UpdatedAt]         DATETIME2        NULL,
    [UpdatedBy]         NVARCHAR(100)    NULL,
    [IsDeleted]         BIT              NOT NULL DEFAULT 0,
    [DeletedAt]         DATETIME2        NULL,
    [DeletedBy]         NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_NotificationTemplates] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_NotificationTemplates_PublicId] UNIQUE ([PublicId]),
    CONSTRAINT [UK_COR_NotificationTemplates_Name_Channel] UNIQUE ([TemplateName], [Channel])
);
GO

-- ============================================================
-- 41. COR_Notifications (nueva — notification log, BIGINT PK)
-- ============================================================
CREATE TABLE [dbo].[COR_Notifications] (
    [Id]                    BIGINT IDENTITY(1,1) NOT NULL,
    [PublicId]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [TemplateId]            INT              NULL,          -- FK → COR_NotificationTemplates
    [RecipientPersonId]     INT              NULL,          -- FK → COR_People
    [Channel]               NVARCHAR(10)     NOT NULL,      -- Email, SMS, Push
    [Subject]               NVARCHAR(500)    NULL,
    [Body]                  NVARCHAR(MAX)    NULL,
    [SentAt]                DATETIME2        NULL,
    [Status]                NVARCHAR(20)     NOT NULL DEFAULT N'Pending', -- Pending, Sent, Failed
    [ErrorMessage]          NVARCHAR(2000)   NULL,
    -- Audit
    [CreatedAt]             DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]             NVARCHAR(100)    NULL,
    [UpdatedAt]             DATETIME2        NULL,
    [UpdatedBy]             NVARCHAR(100)    NULL,
    [IsDeleted]             BIT              NOT NULL DEFAULT 0,
    [DeletedAt]             DATETIME2        NULL,
    [DeletedBy]             NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Notifications] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Notifications_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- 42. COR_Attachments (nueva — file attachment metadata, BIGINT PK)
-- ============================================================
CREATE TABLE [dbo].[COR_Attachments] (
    [Id]                BIGINT IDENTITY(1,1) NOT NULL,
    [PublicId]           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [EntityType]        NVARCHAR(100)    NOT NULL,          -- e.g. 'COR_People', 'COP_Loans', etc.
    [EntityId]          INT              NOT NULL,           -- PK of the related entity
    [FileName]          NVARCHAR(500)    NOT NULL,
    [ContentType]       NVARCHAR(200)    NULL,               -- MIME type
    [FileSize]          BIGINT           NULL,               -- bytes
    [StoragePath]       NVARCHAR(2000)   NOT NULL,           -- relative path or blob key
    [StorageProvider]   NVARCHAR(50)     NOT NULL DEFAULT N'Local', -- Local, AzureBlob, S3
    -- Audit
    [CreatedAt]         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    [CreatedBy]         NVARCHAR(100)    NULL,
    [UpdatedAt]         DATETIME2        NULL,
    [UpdatedBy]         NVARCHAR(100)    NULL,
    [IsDeleted]         BIT              NOT NULL DEFAULT 0,
    [DeletedAt]         DATETIME2        NULL,
    [DeletedBy]         NVARCHAR(100)    NULL,
    CONSTRAINT [PK_COR_Attachments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UK_COR_Attachments_PublicId] UNIQUE ([PublicId])
);
GO

-- ============================================================
-- END OF MODULE: COR_ (Core/Sistema) — 42 tables
-- ============================================================
