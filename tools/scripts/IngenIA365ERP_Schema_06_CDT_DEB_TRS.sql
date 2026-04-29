-- ============================================================
-- IngenIA365ERP — Database Schema v1.0
-- Modules: CDT_ (7), DEB_ (7), TRS_ (3) — 17 tables
-- ============================================================
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- ************************************************************
-- CDT_ — Certificates of Deposit (7 tables)
-- ************************************************************

-- ============================================================
-- 168. CDT_Certificates  (orig: cdt_maecdats)
-- ============================================================
CREATE TABLE [dbo].[CDT_Certificates] (
    Id                      INT             IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CDT_Certificates_PublicId DEFAULT NEWID(),
    CertificateNumber       NVARCHAR(20)    NOT NULL,                   -- num_cdat
    PersonId                INT             NOT NULL,                   -- codigoter
    CreditLineId            INT             NOT NULL,                   -- lincred
    IssueDate               DATE            NOT NULL,                   -- fec_crea
    MaturityDate            DATE            NULL,                       -- Fecvence
    AccrualDate             DATE            NULL,                       -- fec_causacion
    Amount                  DECIMAL(18,2)   NOT NULL CONSTRAINT DF_CDT_Cert_Amount DEFAULT 0,       -- Valorob
    InterestRate            DECIMAL(10,6)   NOT NULL CONSTRAINT DF_CDT_Cert_Rate DEFAULT 0,          -- TasaInt
    Term                    INT             NOT NULL CONSTRAINT DF_CDT_Cert_Term DEFAULT 0,           -- Plazo
    RenewalType             NVARCHAR(2)     NULL,                       -- tipocdat
    Status                  NVARCHAR(2)     NOT NULL,                   -- Estado
    PaymentMethod           NVARCHAR(2)     NULL,                       -- consignainteres
    Periodicity             INT             NULL,                       -- periodicidad
    BranchId                INT             NULL,
    -- Legal representative / contact info
    LegalRepId              NVARCHAR(20)    NULL,                       -- rep_legal
    LegalRepName            NVARCHAR(50)    NULL,                       -- nom_repres
    BusinessAddress         NVARCHAR(50)    NULL,                       -- dir_comerci
    Phone                   NVARCHAR(20)    NULL,                       -- telefono
    Mobile                  NVARCHAR(20)    NULL,                       -- celular
    -- Required signatories
    SignatoryId1            NVARCHAR(20)    NULL,                       -- cc_nit_firmareq1
    SignatoryId2            NVARCHAR(20)    NULL,                       -- cc_nit_firmareq2
    SignatoryId3            NVARCHAR(20)    NULL,                       -- cc_nit_firmareq3
    SignatoryName1          NVARCHAR(50)    NULL,                       -- nom_firmareq1
    SignatoryName2          NVARCHAR(50)    NULL,                       -- nom_firmareq2
    SignatoryName3          NVARCHAR(50)    NULL,                       -- nom_firmareq3
    -- Beneficiaries
    BeneficiaryId1          NVARCHAR(20)    NULL,                       -- cc_nit_benefi1
    BeneficiaryId2          NVARCHAR(20)    NULL,                       -- cc_nit_benefi2
    BeneficiaryId3          NVARCHAR(20)    NULL,                       -- cc_nit_benefi3
    BeneficiaryId4          NVARCHAR(20)    NULL,                       -- cc_nit_benefi4
    BeneficiaryId5          NVARCHAR(20)    NULL,                       -- cc_nit_benefi5
    BeneficiaryName1        NVARCHAR(50)    NULL,                       -- nom_benefi1
    BeneficiaryName2        NVARCHAR(50)    NULL,                       -- nom_benefi2
    BeneficiaryName3        NVARCHAR(50)    NULL,                       -- nom_benefi3
    BeneficiaryName4        NVARCHAR(50)    NULL,                       -- nom_benefi4
    BeneficiaryName5        NVARCHAR(50)    NULL,                       -- nom_benefi5
    BeneficiaryPct1         DECIMAL(6,3)    NOT NULL CONSTRAINT DF_CDT_Cert_BenPct1 DEFAULT 0,
    BeneficiaryPct2         DECIMAL(6,3)    NOT NULL CONSTRAINT DF_CDT_Cert_BenPct2 DEFAULT 0,
    BeneficiaryPct3         DECIMAL(6,3)    NOT NULL CONSTRAINT DF_CDT_Cert_BenPct3 DEFAULT 0,
    BeneficiaryPct4         DECIMAL(6,3)    NOT NULL CONSTRAINT DF_CDT_Cert_BenPct4 DEFAULT 0,
    BeneficiaryPct5         DECIMAL(6,3)    NOT NULL CONSTRAINT DF_CDT_Cert_BenPct5 DEFAULT 0,
    -- Cancellation fields
    CancelledByUserId       NVARCHAR(20)    NULL,                       -- usucancelacdat
    CancellationDate        DATETIME2       NULL,                       -- feccancela
    IsCapitalized           BIT             NOT NULL CONSTRAINT DF_CDT_Cert_IsCap DEFAULT 0,         -- Marca_Capitalizado
    PreviousCertificateId   INT             NULL,                       -- num_cdat_Anterior
    -- Audit columns
    CreatedAt               DATETIME2       NOT NULL CONSTRAINT DF_CDT_Certificates_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(50)    NULL,
    UpdatedAt               DATETIME2       NULL,
    UpdatedBy               NVARCHAR(50)    NULL,
    IsDeleted               BIT             NOT NULL CONSTRAINT DF_CDT_Certificates_IsDeleted DEFAULT 0,
    DeletedAt               DATETIME2       NULL,
    DeletedBy               NVARCHAR(50)    NULL,
    CONSTRAINT PK_CDT_Certificates PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_CDT_Certificates_PublicId       UNIQUE (PublicId),
    CONSTRAINT UK_CDT_Certificates_CertNumber     UNIQUE (CertificateNumber)
);
GO

-- ============================================================
-- 169. CDT_CertificateEntries  (orig: cdt_novcdats)
-- ============================================================
CREATE TABLE [dbo].[CDT_CertificateEntries] (
    Id                  BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CDT_CertEntries_PublicId DEFAULT NEWID(),
    CertificateId       INT             NOT NULL,                       -- numcdat
    PersonId            INT             NOT NULL,                       -- codigoter
    CreditLineId        INT             NOT NULL,                       -- lincred
    EntryDate           DATE            NOT NULL,                       -- FechaNovedad
    OpeningDate         DATE            NULL,                           -- FechaApertura
    EntryType           NVARCHAR(5)     NOT NULL,                       -- TipoNovedad
    PreviousRate        DECIMAL(12,3)   NOT NULL CONSTRAINT DF_CDT_CertEntries_PrevRate DEFAULT 0,
    CurrentRate         DECIMAL(12,3)   NOT NULL CONSTRAINT DF_CDT_CertEntries_CurrRate DEFAULT 0,
    Amount              DECIMAL(18,2)   NOT NULL CONSTRAINT DF_CDT_CertEntries_Amount DEFAULT 0,    -- ValorCdat
    Description         NVARCHAR(200)   NULL,
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_CDT_CertEntries_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_CDT_CertEntries_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_CDT_CertificateEntries PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_CDT_CertificateEntries_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 170. CDT_Parameters  (orig: cdt_parame58)
-- ============================================================
CREATE TABLE [dbo].[CDT_Parameters] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CDT_Parameters_PublicId DEFAULT NEWID(),
    CreditLineId        INT             NOT NULL,                       -- lincred
    Description         NVARCHAR(100)   NULL,                           -- Descripcion
    MinimumRate         DECIMAL(5,2)    NULL,                           -- TasaMin
    AnnualRate          DECIMAL(12,9)   NULL,                           -- PorAnual
    WithholdingRate     DECIMAL(4,2)    NULL,                           -- PorRetfte
    MinWithholdingAmount DECIMAL(18,2)  NULL,                           -- VlrMinRet
    InterestConceptId   INT             NULL,                           -- CptoInt
    WithholdingConceptId INT            NULL,                           -- CptoRet
    MonthlyIncrement    DECIMAL(4,2)    NULL,                           -- IncreMen
    InterestRate        DECIMAL(10,5)   NULL,
    Term                INT             NULL,
    MinAmount           DECIMAL(18,2)   NULL,
    MaxAmount           DECIMAL(18,2)   NULL,
    InterestPaymentType INT             NOT NULL CONSTRAINT DF_CDT_Params_ForPag DEFAULT 0,          -- ForPagInt
    InterestType        NVARCHAR(2)     NOT NULL CONSTRAINT DF_CDT_Params_IntType DEFAULT 'S',       -- tipointeres
    FormatId            INT             NOT NULL CONSTRAINT DF_CDT_Params_FmtId DEFAULT 0,
    ConceptId           INT             NOT NULL CONSTRAINT DF_CDT_Params_CptoId DEFAULT 0,
    SourceId            INT             NOT NULL CONSTRAINT DF_CDT_Params_SrcId DEFAULT 0,
    TreasuryAccount     NVARCHAR(15)    NULL,                           -- CuentaTesoreria
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_CDT_Parameters_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_CDT_Parameters_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_CDT_Parameters PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_CDT_Parameters_PublicId      UNIQUE (PublicId),
    CONSTRAINT UK_CDT_Parameters_CreditLineId  UNIQUE (CreditLineId)
);
GO

-- ============================================================
-- 171. CDT_RatesByTerm  (orig: cdt_tasasplazos)
-- ============================================================
CREATE TABLE [dbo].[CDT_RatesByTerm] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CDT_RatesByTerm_PublicId DEFAULT NEWID(),
    CreditLineId        INT             NOT NULL,                       -- lincred
    AmountRangeStart    DECIMAL(18,2)   NOT NULL,                       -- vlrinicial
    AmountRangeEnd      DECIMAL(18,2)   NOT NULL,                       -- vlrfinal
    TermStart           INT             NOT NULL,                       -- plazoinicial
    TermEnd             INT             NOT NULL,                       -- plazofinal
    InterestRate        DECIMAL(10,5)   NULL,                           -- tasa
    LastUpdated         DATETIME2       NULL,                           -- fecactualizacion
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_CDT_RatesByTerm_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_CDT_RatesByTerm_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_CDT_RatesByTerm PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_CDT_RatesByTerm_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_CDT_RatesByTerm_Composite UNIQUE (CreditLineId, AmountRangeStart, AmountRangeEnd, TermStart, TermEnd)
);
GO

-- ============================================================
-- 172. CDT_AssociateReferences  (orig: cdt_asoreferencia)
-- ============================================================
CREATE TABLE [dbo].[CDT_AssociateReferences] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CDT_AssoRef_PublicId DEFAULT NEWID(),
    CertificateId       INT             NOT NULL,                       -- NumCdat
    PersonId            INT             NOT NULL,                       -- codigoter
    CreditLineId        INT             NOT NULL,                       -- lincred
    RelationshipType    NVARCHAR(5)     NOT NULL,                       -- TipoReferencia
    ReferenceName       NVARCHAR(100)   NOT NULL,                       -- Nombre
    ReferenceAddress    NVARCHAR(100)   NULL,                           -- direccion
    CityId              INT             NULL,                           -- ciudad
    ReferencePhone      NVARCHAR(30)    NULL,                           -- telefono
    ReferenceMobile     NVARCHAR(30)    NULL,                           -- Celular
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_CDT_AssoRef_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_CDT_AssoRef_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_CDT_AssociateReferences PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_CDT_AssociateReferences_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 173. CDT_Audit  (orig: cdt_maeaud)
-- ============================================================
CREATE TABLE [dbo].[CDT_Audit] (
    Id                          BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CDT_Audit_PublicId DEFAULT NEWID(),
    Action                      NVARCHAR(1)     NOT NULL,               -- Accion
    ActionDate                  DATETIME2       NULL,                   -- fechasys
    UserName                    NVARCHAR(50)    NULL,                   -- usuario_act
    EntityType                  NVARCHAR(50)    NULL,
    EntityId                    INT             NULL,                   -- num_cdat
    PersonId                    INT             NULL,                   -- codigoter
    CreditLineId                INT             NULL,                   -- lincred
    -- Old/New value pairs for key fields
    IssueDateOld                DATE            NULL,                   -- fec_crea_ant
    IssueDateNew                DATE            NULL,                   -- fec_crea_act
    LegalRepIdOld               NVARCHAR(20)    NULL,                   -- rep_legal_ant
    LegalRepIdNew               NVARCHAR(20)    NULL,                   -- rep_legal_act
    LegalRepNameOld             NVARCHAR(50)    NULL,                   -- nom_repres_ant
    LegalRepNameNew             NVARCHAR(50)    NULL,                   -- nom_repres_act
    AddressOld                  NVARCHAR(50)    NULL,                   -- dir_comerci_ant
    AddressNew                  NVARCHAR(50)    NULL,                   -- dir_comerci_act
    PhoneOld                    NVARCHAR(20)    NULL,                   -- telefono_ant
    PhoneNew                    NVARCHAR(20)    NULL,                   -- telefono_act
    MobileOld                   NVARCHAR(20)    NULL,                   -- celular_ant
    MobileNew                   NVARCHAR(20)    NULL,                   -- celular_act
    StatusOld                   NVARCHAR(2)     NULL,                   -- Estado_ant
    StatusNew                   NVARCHAR(2)     NULL,                   -- Estado_act
    AccrualDateOld              DATE            NULL,                   -- fec_causacion_ant
    AccrualDateNew              DATE            NULL,                   -- fec_causacion_act
    MaturityDateOld             DATE            NULL,                   -- Fecvence_ant
    MaturityDateNew             DATE            NULL,                   -- Fecvence_act
    TermOld                     INT             NULL,                   -- Plazo_ant
    TermNew                     INT             NULL,                   -- Plazo_act
    InterestRateOld             DECIMAL(6,3)    NULL,                   -- TasaInt_ant
    InterestRateNew             DECIMAL(6,3)    NULL,                   -- TasaInt_act
    AmountOld                   DECIMAL(18,2)   NULL,                   -- Valorob_ant
    AmountNew                   DECIMAL(18,2)   NULL,                   -- Valorob_act
    CancelledByOld              NVARCHAR(20)    NULL,                   -- usucancelacdat_ant
    CancelledByNew              NVARCHAR(20)    NULL,                   -- usucancelacdat_act
    CancellationDateOld         DATETIME2       NULL,                   -- feccancela_ant
    CancellationDateNew         DATETIME2       NULL,                   -- feccancela_act
    IsCapitalizedOld            NVARCHAR(2)     NULL,                   -- Marca_Capitalizado_ant
    IsCapitalizedNew            NVARCHAR(2)     NULL,                   -- Marca_Capitalizado_act
    PreviousCertIdOld           INT             NULL,                   -- num_cdat_Anterior_ant
    PreviousCertIdNew           INT             NULL,                   -- num_cdat_Anterior_act
    OldValues                   NVARCHAR(MAX)   NULL,
    NewValues                   NVARCHAR(MAX)   NULL,
    -- Audit columns
    CreatedAt                   DATETIME2       NOT NULL CONSTRAINT DF_CDT_Audit_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy                   NVARCHAR(50)    NULL,
    UpdatedAt                   DATETIME2       NULL,
    UpdatedBy                   NVARCHAR(50)    NULL,
    IsDeleted                   BIT             NOT NULL CONSTRAINT DF_CDT_Audit_IsDeleted DEFAULT 0,
    DeletedAt                   DATETIME2       NULL,
    DeletedBy                   NVARCHAR(50)    NULL,
    CONSTRAINT PK_CDT_Audit PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_CDT_Audit_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 174. CDT_ParameterAudit  (orig: cdt_paramaud)
-- ============================================================
CREATE TABLE [dbo].[CDT_ParameterAudit] (
    Id                      BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CDT_ParamAudit_PublicId DEFAULT NEWID(),
    Action                  NVARCHAR(1)     NOT NULL,                   -- Accion
    ActionDate              DATETIME2       NOT NULL,                   -- fechasys
    UserName                NVARCHAR(50)    NULL,                       -- usuario_act
    CreditLineId            INT             NULL,                       -- lincred
    DescriptionOld          NVARCHAR(100)   NULL,                       -- Descripcion_ant
    DescriptionNew          NVARCHAR(100)   NULL,                       -- Descripcion_act
    MinRateOld              DECIMAL(5,2)    NULL,                       -- TasaMin_ant
    MinRateNew              DECIMAL(5,2)    NULL,                       -- TasaMin_act
    AnnualRateOld           DECIMAL(12,9)   NULL,                       -- PorAnual_ant
    AnnualRateNew           DECIMAL(12,9)   NULL,                       -- PorAnual_act
    WithholdingRateOld      DECIMAL(4,2)    NULL,                       -- PorRetfte_ant
    WithholdingRateNew      DECIMAL(4,2)    NULL,                       -- PorRetfte_act
    MinWithholdingAmtOld    DECIMAL(18,2)   NULL,                       -- VlrMinRet_ant
    MinWithholdingAmtNew    DECIMAL(18,2)   NULL,                       -- VlrMinRet_act
    PaymentMethodOld        INT             NULL,                       -- FormaPag_ant
    PaymentMethodNew        INT             NULL,                       -- FormaPag_act
    InterestConceptOld      INT             NULL,                       -- CptoInt_ant
    InterestConceptNew      INT             NULL,                       -- CptoInt_act
    WithholdingConceptOld   INT             NULL,                       -- CptoRet_ant
    WithholdingConceptNew   INT             NULL,                       -- CptoRet_act
    MonthlyIncrementOld     DECIMAL(4,2)    NULL,                       -- IncreMen_ant
    MonthlyIncrementNew     DECIMAL(4,2)    NULL,                       -- IncreMen_act
    OldValues               NVARCHAR(MAX)   NULL,
    NewValues               NVARCHAR(MAX)   NULL,
    -- Audit columns
    CreatedAt               DATETIME2       NOT NULL CONSTRAINT DF_CDT_ParamAudit_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(50)    NULL,
    UpdatedAt               DATETIME2       NULL,
    UpdatedBy               NVARCHAR(50)    NULL,
    IsDeleted               BIT             NOT NULL CONSTRAINT DF_CDT_ParamAudit_IsDeleted DEFAULT 0,
    DeletedAt               DATETIME2       NULL,
    DeletedBy               NVARCHAR(50)    NULL,
    CONSTRAINT PK_CDT_ParameterAudit PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_CDT_ParameterAudit_PublicId UNIQUE (PublicId)
);
GO

-- ************************************************************
-- DEB_ — Debit Cards (7 tables)
-- ************************************************************

-- ============================================================
-- 175. DEB_Cards  (orig: deb_maetarj)
-- ============================================================
CREATE TABLE [dbo].[DEB_Cards] (
    Id                      INT             IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_DEB_Cards_PublicId DEFAULT NEWID(),
    BankId                  INT             NOT NULL,                   -- Banco
    CardNumber              NVARCHAR(25)    NOT NULL,                   -- Tarjeta
    BinCode                 NVARCHAR(10)    NULL,                       -- Bin
    AccountNumber           INT             NULL,                       -- Cuenta
    CreditLineId            INT             NULL,                       -- Lincred
    AccountType             NVARCHAR(5)     NULL,                       -- TipoCta
    ErrorCode               NVARCHAR(5)     NULL,                       -- Error
    PersonId                INT             NULL,                       -- Codigoter
    OperationType           NVARCHAR(5)     NULL,                       -- Operacion
    Status                  NVARCHAR(2)     NULL,                       -- Estado
    InitialConcept          NVARCHAR(20)    NULL,                       -- Cpto_Ini
    AvailableBalance        DECIMAL(18,2)   NULL,                       -- Disponible
    DailyAtmLimit           DECIMAL(18,2)   NULL,                       -- CupoCajero
    DailyAtmTransactions    INT             NULL,                       -- TranCajero
    DailyPosLimit           DECIMAL(18,2)   NULL,                       -- CupoPos
    DailyPosTransactions    INT             NULL,                       -- TranPos
    IssueDate               DATE            NULL,                       -- FecAsignacion
    ExpiryDate              DATE            NULL,                       -- fechavence
    LastEventDate           DATE            NULL,                       -- FecNovedad
    ExecutionTime           INT             NULL,                       -- HoraEjccion
    DownloadDate            DATE            NULL,                       -- FecDscrgue
    Pin                     NVARCHAR(100)   NULL,
    Mark                    INT             NOT NULL CONSTRAINT DF_DEB_Cards_Mark DEFAULT 0,
    AvailableLimitType      INT             NOT NULL CONSTRAINT DF_DEB_Cards_AvailLimType DEFAULT 0,
    AtmLimitType            INT             NULL,                       -- ClaseTopeCajero
    IsDebitOrCredit         NVARCHAR(2)     NOT NULL CONSTRAINT DF_DEB_Cards_DebCre DEFAULT 'D',
    CoSigner1               NVARCHAR(20)    NULL,                       -- Codeudor1
    CoSigner2               NVARCHAR(20)    NULL,                       -- Codeudor2
    CutoffDay               INT             NOT NULL CONSTRAINT DF_DEB_Cards_Cutoff DEFAULT 0,
    CreditLimit             DECIMAL(18,2)   NOT NULL CONSTRAINT DF_DEB_Cards_CreditLim DEFAULT 0,
    TerminalId              NVARCHAR(30)    NULL,                       -- Trmnal
    BlockReasonId           INT             NOT NULL CONSTRAINT DF_DEB_Cards_BlockReason DEFAULT 0,
    BlockedByUserId         NVARCHAR(20)    NULL,                       -- UsuarioBloqueo
    -- Secondary (domestic) limits
    DomesticAvailableBalance DECIMAL(18,2)  NOT NULL CONSTRAINT DF_DEB_Cards_DomAvail DEFAULT 0,
    DomesticAtmLimit        DECIMAL(18,2)   NOT NULL CONSTRAINT DF_DEB_Cards_DomAtm DEFAULT 0,
    DomesticAtmTransactions INT             NOT NULL CONSTRAINT DF_DEB_Cards_DomAtmTrans DEFAULT 0,
    DomesticPosLimit        DECIMAL(18,2)   NOT NULL CONSTRAINT DF_DEB_Cards_DomPos DEFAULT 0,
    DomesticPosTransactions INT             NOT NULL CONSTRAINT DF_DEB_Cards_DomPosTrans DEFAULT 0,
    DomesticAccountNumber   INT             NOT NULL CONSTRAINT DF_DEB_Cards_DomAcct DEFAULT 0,
    ChargesManagement       BIT             NOT NULL CONSTRAINT DF_DEB_Cards_ChrgMgmt DEFAULT 0,
    ChargesManagementDs     BIT             NOT NULL CONSTRAINT DF_DEB_Cards_ChrgMgmtDs DEFAULT 0,
    LegacyLineId            DECIMAL(18,0)   NOT NULL CONSTRAINT DF_DEB_Cards_LegLine DEFAULT 0,
    LimitAssignmentDate     DATE            NULL,                       -- FecAsignaCupo
    -- Audit columns
    CreatedAt               DATETIME2       NOT NULL CONSTRAINT DF_DEB_Cards_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(50)    NULL,
    UpdatedAt               DATETIME2       NULL,
    UpdatedBy               NVARCHAR(50)    NULL,
    IsDeleted               BIT             NOT NULL CONSTRAINT DF_DEB_Cards_IsDeleted DEFAULT 0,
    DeletedAt               DATETIME2       NULL,
    DeletedBy               NVARCHAR(50)    NULL,
    CONSTRAINT PK_DEB_Cards PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_DEB_Cards_PublicId      UNIQUE (PublicId),
    CONSTRAINT UK_DEB_Cards_BankCard      UNIQUE (BankId, CardNumber)
);
GO

-- ============================================================
-- 176. DEB_Transactions  (orig: deb_movto)
-- ============================================================
CREATE TABLE [dbo].[DEB_Transactions] (
    Id                      BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_DEB_Transactions_PublicId DEFAULT NEWID(),
    CardId                  INT             NULL,
    SequenceCode            NVARCHAR(30)    NOT NULL,                   -- Secuencia
    CardNumber              NVARCHAR(25)    NOT NULL,                   -- Tarjeta
    TransactionDate         DATETIME2       NULL,                       -- FechaMovto + Hora
    Amount                  DECIMAL(18,2)   NULL,                       -- Monto
    TransactionType         NVARCHAR(5)     NULL,                       -- Id (causal type)
    CausalCode              NVARCHAR(5)     NULL,                       -- Causal
    Status                  NVARCHAR(2)     NULL,                       -- Estado
    SourceSystem            NVARCHAR(20)    NULL,                       -- Source
    TransactionTime         NVARCHAR(10)    NULL,                       -- Hora
    NetworkCode             NVARCHAR(10)    NULL,                       -- Net
    MessageCode             INT             NULL,                       -- Message
    VatAmount               DECIMAL(18,2)   NULL,                       -- Iva
    VatBase                 DECIMAL(18,2)   NULL,                       -- BaseIva
    CommissionAmount        DECIMAL(18,2)   NULL,                       -- Comision
    MethodCode              INT             NULL,                       -- Metodo
    ErrorCode               NVARCHAR(5)     NULL,                       -- Error
    MerchantCode            NVARCHAR(20)    NULL,
    AuthorizationCode       NVARCHAR(20)    NULL,
    -- Audit columns
    CreatedAt               DATETIME2       NOT NULL CONSTRAINT DF_DEB_Transactions_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(50)    NULL,
    UpdatedAt               DATETIME2       NULL,
    UpdatedBy               NVARCHAR(50)    NULL,
    IsDeleted               BIT             NOT NULL CONSTRAINT DF_DEB_Transactions_IsDeleted DEFAULT 0,
    DeletedAt               DATETIME2       NULL,
    DeletedBy               NVARCHAR(50)    NULL,
    CONSTRAINT PK_DEB_Transactions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_DEB_Transactions_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 177. DEB_AgreementParameters  (orig: deb_parconv)
-- ============================================================
CREATE TABLE [dbo].[DEB_AgreementParameters] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_DEB_AgreementParams_PublicId DEFAULT NEWID(),
    AgreementCode       NVARCHAR(10)    NOT NULL,                       -- idconv
    TokenRS             INT             NOT NULL CONSTRAINT DF_DEB_AgrParams_Token DEFAULT 0,
    Name                NVARCHAR(100)   NULL,
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_DEB_AgreementParams_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_DEB_AgreementParams_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_DEB_AgreementParameters PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_DEB_AgreementParams_PublicId   UNIQUE (PublicId),
    CONSTRAINT UK_DEB_AgreementParams_Code       UNIQUE (AgreementCode)
);
GO

-- ============================================================
-- 178. DEB_PosTerminals  (orig: deb_pardatafonos)
-- ============================================================
CREATE TABLE [dbo].[DEB_PosTerminals] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_DEB_PosTerminals_PublicId DEFAULT NEWID(),
    TerminalCode        NVARCHAR(20)    NOT NULL,                       -- terminal
    InternalCode        INT             NOT NULL,                       -- idcodigo
    VoucherCode         NVARCHAR(5)     NULL,                           -- cpte
    MerchantName        NVARCHAR(100)   NULL,
    Location            NVARCHAR(100)   NULL,
    Status              NVARCHAR(2)     NULL,
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_DEB_PosTerminals_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_DEB_PosTerminals_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_DEB_PosTerminals PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_DEB_PosTerminals_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 179. DEB_DailyParameters  (orig: deb_pardiario)
-- ============================================================
CREATE TABLE [dbo].[DEB_DailyParameters] (
    Id                      INT             IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_DEB_DailyParams_PublicId DEFAULT NEWID(),
    ParameterCode           INT             NOT NULL,                   -- IdCodigo
    BankId                  NVARCHAR(10)    NOT NULL,                   -- Banco
    BatchVoucherCode        NVARCHAR(5)     NOT NULL,                   -- Cptebatch
    OnlineVoucherCode       NVARCHAR(5)     NOT NULL,                   -- CpteLinea
    Description             NVARCHAR(50)    NOT NULL,                   -- Detalle
    LastUpdateDate          DATE            NULL,                       -- fechaActualizacion
    NewCardsCount           INT             NOT NULL CONSTRAINT DF_DEB_DailyParams_NewCards DEFAULT 0,
    LastCardNumber          NVARCHAR(25)    NULL,                       -- UltimaTarjeta
    ClosingDate             DATE            NULL,                       -- FechaCierre
    ClosingVoucherCode      NVARCHAR(5)     NULL,                       -- CpteCierre
    ClosingSequenceNumber   BIGINT          NOT NULL CONSTRAINT DF_DEB_DailyParams_CloseSeq DEFAULT 0,
    PosClosingVoucherCode   NVARCHAR(5)     NULL,                       -- CpteCierreDatafono
    PosClosingSequence      INT             NOT NULL CONSTRAINT DF_DEB_DailyParams_PosCloseSeq DEFAULT 0,
    ClosingFlag             NVARCHAR(2)     NULL,                       -- cierre
    NetworkCommission       DECIMAL(18,2)   NOT NULL CONSTRAINT DF_DEB_DailyParams_NetComm DEFAULT 0,
    OtherNetworkCommission  DECIMAL(18,2)   NOT NULL CONSTRAINT DF_DEB_DailyParams_OtherNetComm DEFAULT 0,
    -- Audit columns
    CreatedAt               DATETIME2       NOT NULL CONSTRAINT DF_DEB_DailyParams_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(50)    NULL,
    UpdatedAt               DATETIME2       NULL,
    UpdatedBy               NVARCHAR(50)    NULL,
    IsDeleted               BIT             NOT NULL CONSTRAINT DF_DEB_DailyParams_IsDeleted DEFAULT 0,
    DeletedAt               DATETIME2       NULL,
    DeletedBy               NVARCHAR(50)    NULL,
    CONSTRAINT PK_DEB_DailyParameters PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_DEB_DailyParams_PublicId       UNIQUE (PublicId),
    CONSTRAINT UK_DEB_DailyParams_ParamCode      UNIQUE (ParameterCode)
);
GO

-- ============================================================
-- 180. DEB_Agreements  (orig: deb_enpacto)
-- ============================================================
CREATE TABLE [dbo].[DEB_Agreements] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_DEB_Agreements_PublicId DEFAULT NEWID(),
    BatchId             BIGINT          NOT NULL,                       -- id
    ProcessDate         NVARCHAR(10)    NOT NULL,                       -- FECH
    ProcessTime         NVARCHAR(10)    NULL,                           -- HORA
    CardNumber          NVARCHAR(25)    NOT NULL,                       -- TARJ
    AuthCode            NVARCHAR(20)    NULL,                           -- NAUD
    AccountNumber       NVARCHAR(30)    NULL,                           -- CUEN
    NetworkCode         NVARCHAR(2)     NULL,                           -- NETW
    TransactionType     NVARCHAR(2)     NULL,                           -- TIPO
    Amount              DECIMAL(18,2)   NULL,                           -- MONT
    ConceptCode         NVARCHAR(2)     NULL,                           -- CONC
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_DEB_Agreements_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_DEB_Agreements_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_DEB_Agreements PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_DEB_Agreements_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 181. DEB_AgreementMembers  (orig: deb_enpactors)
-- ============================================================
CREATE TABLE [dbo].[DEB_AgreementMembers] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_DEB_AgrMembers_PublicId DEFAULT NEWID(),
    ProcessDate         DATE            NOT NULL,                       -- fecha
    UserName            NVARCHAR(20)    NOT NULL,                       -- usuario
    VoucherCode         NVARCHAR(5)     NOT NULL,                       -- comprobante
    DocumentNumber      BIGINT          NOT NULL,                       -- numero_domto
    IsApplied           BIT             NOT NULL CONSTRAINT DF_DEB_AgrMembers_Applied DEFAULT 0,
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_DEB_AgrMembers_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_DEB_AgrMembers_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_DEB_AgreementMembers PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_DEB_AgreementMembers_PublicId UNIQUE (PublicId)
);
GO

-- ************************************************************
-- TRS_ — Treasury (3 tables)
-- ************************************************************

-- ============================================================
-- 233. TRS_Checks  (orig: TES_CHEQUES)
-- ============================================================
CREATE TABLE [dbo].[TRS_Checks] (
    Id                  BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_TRS_Checks_PublicId DEFAULT NEWID(),
    ConceptCode         NVARCHAR(5)     NOT NULL,                       -- CONCEPTO
    BankId              INT             NOT NULL,                       -- BANCO
    SequentialNumber    INT             NOT NULL,                       -- CONSECUTIVO
    PersonId            INT             NULL,                           -- NIT
    VoucherCode         NVARCHAR(5)     NULL,                           -- COMPROBANTE
    VoucherNumber       INT             NULL,                           -- NUME_COMPRO
    CheckDate           DATE            NOT NULL,                       -- FECHA_CHEQUE
    CheckNumber         INT             NOT NULL,                       -- NUMERO_CHEQUE
    Amount              DECIMAL(18,2)   NOT NULL CONSTRAINT DF_TRS_Checks_Amount DEFAULT 0,
    VoidDetail          NVARCHAR(50)    NULL,                           -- DETALLE_ANULA
    VoidUserId          NVARCHAR(20)    NULL,                           -- USUARIO_ANULA
    VoidVoucherCode     NVARCHAR(5)     NULL,                           -- COMPRO_ANULA
    VoidVoucherNumber   INT             NULL,                           -- NUMCOMP_ANULA
    VoidDate            DATETIME2       NULL,                           -- FECHA_ANULA
    RecordUserId        NVARCHAR(20)    NULL,                           -- USUARIO_GRABA
    RecordDate          DATETIME2       NULL,                           -- FECHASYS_GRABA
    Status              NVARCHAR(2)     NULL,                           -- ESTADO
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_TRS_Checks_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_TRS_Checks_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_TRS_Checks PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_TRS_Checks_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_TRS_Checks_ConceptBankSeq UNIQUE (ConceptCode, BankId, SequentialNumber)
);
GO

-- ============================================================
-- 234. TRS_Concepts  (orig: TES_CPTOS)
-- ============================================================
CREATE TABLE [dbo].[TRS_Concepts] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_TRS_Concepts_PublicId DEFAULT NEWID(),
    ConceptCode         NVARCHAR(5)     NOT NULL,                       -- CONCEPTO
    Name                NVARCHAR(60)    NOT NULL,                       -- NOMBRE
    ShortName           NVARCHAR(20)    NULL,                           -- NOMRES
    ConceptType         NVARCHAR(5)     NULL,                           -- TIPO_CPTO
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_TRS_Concepts_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_TRS_Concepts_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_TRS_Concepts PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_TRS_Concepts_PublicId     UNIQUE (PublicId),
    CONSTRAINT UK_TRS_Concepts_ConceptCode  UNIQUE (ConceptCode)
);
GO

-- ============================================================
-- 235. TRS_Invoices  (orig: TES_FACTURA)
-- ============================================================
CREATE TABLE [dbo].[TRS_Invoices] (
    Id                  BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_TRS_Invoices_PublicId DEFAULT NEWID(),
    ConceptCode         NVARCHAR(5)     NOT NULL,                       -- CONCEPTO
    ConsecutiveNumber   BIGINT          NOT NULL,                       -- CONSECUTIVO
    EntryDate           DATE            NOT NULL,                       -- FECHA
    PeriodCode          INT             NULL,                           -- PERIODO
    InvoiceNumber       NVARCHAR(20)    NOT NULL,                       -- FACTURA
    InvoiceDate         DATE            NOT NULL,                       -- FECHA_FAC
    PersonId            INT             NOT NULL,                       -- NIT / CODIGOTER
    DueDate             DATE            NULL,                           -- FEC_VEMTO
    AccountCode         NVARCHAR(15)    NULL,                           -- CUENTA_CONTABLE
    CostCenterId        NVARCHAR(10)    NULL,                           -- CCOSTO
    BranchId            NVARCHAR(10)    NULL,                           -- AGENCIA
    DocumentCode        NVARCHAR(15)    NULL,                           -- DOCUMNTO
    Description         NVARCHAR(200)   NULL,                           -- DETALLE
    Amount              DECIMAL(18,2)   NOT NULL CONSTRAINT DF_TRS_Invoices_Amount DEFAULT 0,
    ScheduledDate       DATE            NULL,                           -- FEC_PROGRAMA
    PaymentDate         DATE            NULL,                           -- FEC_PAGO
    VoucherCode         NVARCHAR(5)     NULL,                           -- COMBTE
    VoucherNumber       INT             NULL,                           -- NUME_CPMPTO
    BankId              NVARCHAR(10)    NULL,                           -- BANCO
    CheckNumber         INT             NULL,                           -- NUMERO_CHEQUE
    CheckAmount         DECIMAL(18,2)   NOT NULL CONSTRAINT DF_TRS_Invoices_CheckAmt DEFAULT 0,
    Status              NVARCHAR(2)     NULL,                           -- ESTADO
    DocumentType        NVARCHAR(5)     NULL,                           -- docu_tipo
    DocumentNumber      NVARCHAR(20)    NULL,                           -- docu_numero
    PaymentForm         NVARCHAR(5)     NULL,                           -- forpag
    HasCommission       BIT             NULL,                           -- comision
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_TRS_Invoices_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_TRS_Invoices_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_TRS_Invoices PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_TRS_Invoices_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_TRS_Invoices_InvPersonAcct UNIQUE (InvoiceNumber, PersonId, AccountCode)
);
GO

-- ============================================================
-- END OF FILE: CDT_ (7) + DEB_ (7) + TRS_ (3) = 17 tables
-- ============================================================
