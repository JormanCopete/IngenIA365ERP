-- ============================================================
-- IngenIA365ERP — Database Schema v1.0
-- Module: INV_ (Inventory) — 24 tables
-- ============================================================
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- 209. INV_Products  (orig: inv_productos)
-- ============================================================
CREATE TABLE [dbo].[INV_Products] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_Products_PublicId DEFAULT NEWID(),
    ProductCode         INT             NOT NULL,                       -- IdProducto
    Name                NVARCHAR(150)   NOT NULL,                       -- Descripcion
    ShortName           NVARCHAR(80)    NULL,                           -- Resumido
    GroupId             INT             NULL,                           -- IdGruProducto
    DiscountTypeId      INT             NULL,                           -- IdTipoDsto
    UnitOfMeasure       NVARCHAR(10)    NULL,                           -- Medida
    CostPrice           DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Products_CostPrice DEFAULT 0,   -- CostoPro
    SalePrice           DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Products_SalePrice DEFAULT 0,    -- PreVenta (from inv_precios)
    VatRate             DECIMAL(6,3)    NOT NULL CONSTRAINT DF_INV_Products_VatRate DEFAULT 0,       -- TasaIva
    MinStock            INT             NOT NULL CONSTRAINT DF_INV_Products_MinStock DEFAULT 0,      -- stockminimo
    MaxStock            INT             NOT NULL CONSTRAINT DF_INV_Products_MaxStock DEFAULT 0,      -- stockmaximo
    CurrentStock        INT             NOT NULL CONSTRAINT DF_INV_Products_CurrentStock DEFAULT 0,  -- CantDisp
    IsActive            BIT             NOT NULL CONSTRAINT DF_INV_Products_IsActive DEFAULT 1,
    Barcode             NVARCHAR(30)    NULL,                           -- CodigoBarras
    OtherTax            DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_Products_OtherTax DEFAULT 0,     -- otrosimp
    ControlsStock       BIT             NOT NULL CONSTRAINT DF_INV_Products_ControlsStock DEFAULT 0, -- ctrlexistencia
    RestrictsLimit      BIT             NOT NULL CONSTRAINT DF_INV_Products_RestrictsLimit DEFAULT 0, -- rstingLimeteVentas
    MaxSalesQuantity    INT             NOT NULL CONSTRAINT DF_INV_Products_MaxSalesQty DEFAULT 0,   -- cantRestringVentas
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_Products_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_Products_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_Products PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_Products_PublicId     UNIQUE (PublicId),
    CONSTRAINT UK_INV_Products_ProductCode  UNIQUE (ProductCode)
);
GO

-- ============================================================
-- 210. INV_ProductGroups  (orig: inv_grupos)
-- ============================================================
CREATE TABLE [dbo].[INV_ProductGroups] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_ProductGroups_PublicId DEFAULT NEWID(),
    GroupCode           INT             NOT NULL,                       -- IdGruProducto
    Name                NVARCHAR(100)   NOT NULL,                       -- Descripcion
    ShortName           NVARCHAR(50)    NULL,                           -- Resumido
    SecondaryGroupId    INT             NULL,                           -- idgruposecund
    RestrictsLimit      BIT             NOT NULL CONSTRAINT DF_INV_ProductGroups_RestrictsLimit DEFAULT 0,
    MaxSalesQuantity    INT             NOT NULL CONSTRAINT DF_INV_ProductGroups_MaxSalesQty DEFAULT 0,
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_ProductGroups_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_ProductGroups_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_ProductGroups PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_ProductGroups_PublicId   UNIQUE (PublicId),
    CONSTRAINT UK_INV_ProductGroups_GroupCode  UNIQUE (GroupCode)
);
GO

-- ============================================================
-- 211. INV_PrimaryGroups  (orig: inv_Grupo_Primario)
-- ============================================================
CREATE TABLE [dbo].[INV_PrimaryGroups] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_PrimaryGroups_PublicId DEFAULT NEWID(),
    GroupCode           INT             NOT NULL,                       -- idgrupo
    Name                NVARCHAR(100)   NOT NULL,                       -- descripcion
    ShortName           NVARCHAR(50)    NULL,                           -- resumido
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_PrimaryGroups_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_PrimaryGroups_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_PrimaryGroups PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_PrimaryGroups_PublicId   UNIQUE (PublicId),
    CONSTRAINT UK_INV_PrimaryGroups_GroupCode  UNIQUE (GroupCode)
);
GO

-- ============================================================
-- 212. INV_SecondaryGroups  (orig: inv_GrupoSecundario)
-- ============================================================
CREATE TABLE [dbo].[INV_SecondaryGroups] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_SecondaryGroups_PublicId DEFAULT NEWID(),
    GroupCode           INT             NOT NULL,                       -- idgrupo
    Name                NVARCHAR(100)   NOT NULL,                       -- descripcion
    ShortName           NVARCHAR(50)    NULL,                           -- resumido
    PrimaryGroupId      INT             NULL,                           -- idgrupoprimario
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_SecondaryGroups_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_SecondaryGroups_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_SecondaryGroups PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_SecondaryGroups_PublicId   UNIQUE (PublicId),
    CONSTRAINT UK_INV_SecondaryGroups_GroupCode  UNIQUE (GroupCode)
);
GO

-- ============================================================
-- 213. INV_Transactions  (orig: inv_movtos)
-- ============================================================
CREATE TABLE [dbo].[INV_Transactions] (
    Id                  BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_Transactions_PublicId DEFAULT NEWID(),
    TransactionTypeId   INT             NOT NULL,                       -- IdTipoMovto
    SequenceNumber      DECIMAL(18,0)   NOT NULL,                       -- Secuencia
    TransactionDate     DATE            NOT NULL,                       -- FecMovto
    InvoiceNumber       NVARCHAR(20)    NULL,                           -- NumFactura
    ProductId           INT             NOT NULL,                       -- IdProducto
    Quantity            INT             NOT NULL CONSTRAINT DF_INV_Transactions_Qty DEFAULT 0,
    VatRate             DECIMAL(6,3)    NOT NULL CONSTRAINT DF_INV_Transactions_VatRate DEFAULT 0,
    DiscountRate        DECIMAL(6,3)    NOT NULL CONSTRAINT DF_INV_Transactions_DsctoRate DEFAULT 0,
    CostFlag            TINYINT         NOT NULL CONSTRAINT DF_INV_Transactions_CostFlag DEFAULT 0,
    SystemDate          DATETIME2       NOT NULL CONSTRAINT DF_INV_Transactions_SysDate DEFAULT SYSUTCDATETIME(),
    CustomerId          INT             NULL,                           -- IdCliente
    VatAmount           DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Transactions_VatAmt DEFAULT 0,
    DiscountAmount      DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Transactions_DsctoAmt DEFAULT 0,
    UnitPrice           DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Transactions_UnitPrice DEFAULT 0,
    UserId              NVARCHAR(20)    NULL,                           -- Idusuario
    SalesPointId        INT             NULL,                           -- IdPunto
    ShiftId             INT             NULL,                           -- IdTurno
    SubTotal            DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Transactions_SubTotal DEFAULT 0,
    NetTotal            DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Transactions_NetTotal DEFAULT 0,
    AdminFee            DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_Transactions_AdminFee DEFAULT 0,
    AdminVat            DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_Transactions_AdminVat DEFAULT 0,
    TicketVat           DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_Transactions_TicketVat DEFAULT 0,
    OtherTax            DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_Transactions_OtherTax DEFAULT 0,
    AirportTax          DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_Transactions_AirportTax DEFAULT 0,
    FuelTax             DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_Transactions_FuelTax DEFAULT 0,
    IsPosTransaction    BIT             NOT NULL CONSTRAINT DF_INV_Transactions_IsPos DEFAULT 0,
    WarehouseId         INT             NULL,                           -- idbodega
    LocationId          INT             NULL,                           -- idubicacion
    ConsecutiveNumber   BIGINT          NOT NULL CONSTRAINT DF_INV_Transactions_Consec DEFAULT 0,
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_Transactions_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_Transactions_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_Transactions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_Transactions_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 214. INV_OrderTransactions  (orig: inv_movtos_orden)
-- ============================================================
CREATE TABLE [dbo].[INV_OrderTransactions] (
    Id                  BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_OrderTransactions_PublicId DEFAULT NEWID(),
    TransactionTypeId   INT             NOT NULL,                       -- IdTipoMovto
    SequenceNumber      DECIMAL(18,0)   NOT NULL,                       -- Secuencia
    TransactionDate     DATE            NOT NULL,                       -- FecMovto
    InvoiceNumber       NVARCHAR(20)    NULL,                           -- NumFactura
    ProductId           INT             NOT NULL,                       -- Idproducto
    Quantity            DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderTrans_Qty DEFAULT 0,
    VatRate             DECIMAL(6,3)    NOT NULL CONSTRAINT DF_INV_OrderTrans_VatRate DEFAULT 0,
    DiscountRate        DECIMAL(6,3)    NOT NULL CONSTRAINT DF_INV_OrderTrans_DsctoRate DEFAULT 0,
    CostAmount          DECIMAL(16,2)   NOT NULL CONSTRAINT DF_INV_OrderTrans_CostAmt DEFAULT 0,
    SystemDate          DATETIME2       NOT NULL CONSTRAINT DF_INV_OrderTrans_SysDate DEFAULT SYSUTCDATETIME(),
    CustomerId          INT             NULL,                           -- IdCliente
    VatAmount           DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderTrans_VatAmt DEFAULT 0,
    DiscountAmount      DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderTrans_DsctoAmt DEFAULT 0,
    UnitPrice           DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_OrderTrans_UnitPrice DEFAULT 0,
    UserId              NVARCHAR(20)    NULL,                           -- Idusuario
    SalesPointId        INT             NULL,                           -- IdPunto
    ShiftId             INT             NULL,                           -- IdTurno
    SubTotal            DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderTrans_SubTotal DEFAULT 0,
    NetTotal            DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderTrans_NetTotal DEFAULT 0,
    PeriodCode          INT             NULL,                           -- periodo
    SaleType            NVARCHAR(5)     NULL,                           -- TipoVenta
    MovementClass       NVARCHAR(5)     NULL,                           -- ClaMovto
    AdminFee            DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_OrderTrans_AdminFee DEFAULT 0,
    AdminVat            DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_OrderTrans_AdminVat DEFAULT 0,
    TicketVat           DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_OrderTrans_TicketVat DEFAULT 0,
    OtherTax            DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_OrderTrans_OtherTax DEFAULT 0,
    AirportTax          DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_OrderTrans_AirportTax DEFAULT 0,
    FuelTax             DECIMAL(10,2)   NOT NULL CONSTRAINT DF_INV_OrderTrans_FuelTax DEFAULT 0,
    WithholdingRate     DECIMAL(6,3)    NOT NULL CONSTRAINT DF_INV_OrderTrans_WRate DEFAULT 0,       -- retfte
    WithholdingAmount   DECIMAL(15,2)   NOT NULL CONSTRAINT DF_INV_OrderTrans_WAmt DEFAULT 0,        -- VlrRetfte
    IcaAmount           DECIMAL(15,2)   NOT NULL CONSTRAINT DF_INV_OrderTrans_IcaAmt DEFAULT 0,      -- Vlrica
    IcaRate             DECIMAL(6,3)    NOT NULL CONSTRAINT DF_INV_OrderTrans_IcaRate DEFAULT 0,      -- tasaica
    IsPosTransaction    BIT             NOT NULL CONSTRAINT DF_INV_OrderTrans_IsPos DEFAULT 0,
    WarehouseId         INT             NULL,                           -- idbodega
    LocationId          INT             NULL,                           -- idubicacion
    ConsecutiveNumber   BIGINT          NOT NULL CONSTRAINT DF_INV_OrderTrans_Consec DEFAULT 0,
    IsOrderApplied      BIT             NOT NULL CONSTRAINT DF_INV_OrderTrans_Applied DEFAULT 0,     -- aplica_orden
    TransferRecord      NVARCHAR(100)   NULL,                           -- RegistroTrasab
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_OrderTrans_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_OrderTrans_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_OrderTransactions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_OrderTransactions_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 215. INV_TransactionTypes  (orig: inv_tipomovtos)
-- ============================================================
CREATE TABLE [dbo].[INV_TransactionTypes] (
    Id                          INT             IDENTITY(1,1) NOT NULL,
    PublicId                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_TransTypes_PublicId DEFAULT NEWID(),
    TypeCode                    INT             NOT NULL,                       -- IdTipoMovto
    Description                 NVARCHAR(150)   NOT NULL,                       -- Descripcion
    ShortDescription            NVARCHAR(80)    NULL,                           -- Resumido
    TransactionVoucherCode      NVARCHAR(5)     NULL,                           -- CpteTran
    CostVoucherCode             NVARCHAR(5)     NULL,                           -- CpteCost
    SequenceNumber              DECIMAL(18,0)   NOT NULL CONSTRAINT DF_INV_TransTypes_Seq DEFAULT 0,
    ControlsStock               INT             NOT NULL CONSTRAINT DF_INV_TransTypes_CtrlStock DEFAULT 0,
    DocumentClass               NVARCHAR(5)     NULL,                           -- ClaDoc
    UpdatesAccounting           INT             NOT NULL CONSTRAINT DF_INV_TransTypes_UpdAcct DEFAULT 0,
    PortfolioVoucherCode        NVARCHAR(5)     NULL,                           -- CpteCartera
    CreditLineId                INT             NULL,                           -- Lincred
    DeductionType               NVARCHAR(2)     NULL,                           -- clades
    InvoiceControl              NVARCHAR(5)     NULL,                           -- ctrlfactura
    TotalInPurchase             BIT             NOT NULL CONSTRAINT DF_INV_TransTypes_TotalPurch DEFAULT 0,
    CostsProducts               BIT             NOT NULL CONSTRAINT DF_INV_TransTypes_Costea DEFAULT 0,
    IsReturn                    BIT             NOT NULL CONSTRAINT DF_INV_TransTypes_IsReturn DEFAULT 0,
    TransfersAccounting         BIT             NOT NULL CONSTRAINT DF_INV_TransTypes_TransAcct DEFAULT 0,
    OrderPedido_SustainPrice    BIT             NOT NULL CONSTRAINT DF_INV_TransTypes_SustPrice DEFAULT 0,
    AllowsBonus                 BIT             NOT NULL CONSTRAINT DF_INV_TransTypes_Bonus DEFAULT 0,
    ValidatesCreditLimit        BIT             NOT NULL CONSTRAINT DF_INV_TransTypes_ValCred DEFAULT 0,
    -- Audit columns
    CreatedAt                   DATETIME2       NOT NULL CONSTRAINT DF_INV_TransTypes_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy                   NVARCHAR(50)    NULL,
    UpdatedAt                   DATETIME2       NULL,
    UpdatedBy                   NVARCHAR(50)    NULL,
    IsDeleted                   BIT             NOT NULL CONSTRAINT DF_INV_TransTypes_IsDeleted DEFAULT 0,
    DeletedAt                   DATETIME2       NULL,
    DeletedBy                   NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_TransactionTypes PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_TransactionTypes_PublicId   UNIQUE (PublicId),
    CONSTRAINT UK_INV_TransactionTypes_TypeCode   UNIQUE (TypeCode)
);
GO

-- ============================================================
-- 216. INV_Invoices  (orig: inv_facturas)
-- ============================================================
CREATE TABLE [dbo].[INV_Invoices] (
    Id                      BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_Invoices_PublicId DEFAULT NEWID(),
    InvoiceCode             NVARCHAR(10)    NOT NULL,                   -- IdCodigo
    Resolution              NVARCHAR(20)    NULL,                       -- Resolucion
    ResolutionDate          DATE            NULL,                       -- FecResol
    VatRegime               INT             NOT NULL CONSTRAINT DF_INV_Invoices_VatRegime DEFAULT 0,
    Prefix                  NVARCHAR(10)    NULL,                       -- Prefijo
    InitialNumber           NVARCHAR(20)    NULL,                       -- NumInicial
    FinalNumber             NVARCHAR(20)    NULL,                       -- NumFinal
    InvoiceConsecutive      BIGINT          NOT NULL CONSTRAINT DF_INV_Invoices_Consec DEFAULT 0,
    EmployeeVoucherCode     NVARCHAR(5)     NULL,                       -- CpteCartEmpl
    EmployeeCreditLineId    INT             NULL,                       -- lincredEmpl
    EmployeeDeductionType   NVARCHAR(2)     NULL,                       -- cladesEmpl
    EmployeeTerm            INT             NULL,                       -- plazoEmpl
    EmployerVoucherCode     NVARCHAR(5)     NULL,                       -- CpteCartPatro
    EmployerCreditLineId    INT             NULL,                       -- lincredPatro
    EmployerDeductionType   NVARCHAR(2)     NULL,                       -- cladesPatro
    EmployerTerm            INT             NULL,                       -- plazoPatro
    AdjustmentTypeId        INT             NULL,                       -- TipMovAjuInv
    PortfolioVoucherCode    NVARCHAR(5)     NULL,                       -- CpteCartera
    CreditLineId            INT             NULL,                       -- lincred
    DeductionType           NVARCHAR(2)     NULL,                       -- clades
    Term                    INT             NULL,                       -- plazo
    UpdatesCosts            INT             NOT NULL CONSTRAINT DF_INV_Invoices_UpdCosts DEFAULT 0,
    PrintsBonusTickets      BIT             NOT NULL CONSTRAINT DF_INV_Invoices_PrintBonus DEFAULT 0,
    OpensRegister           BIT             NOT NULL CONSTRAINT DF_INV_Invoices_OpensReg DEFAULT 0,
    CommissionGroup         NVARCHAR(2)     NULL,                       -- grupocomision
    CommissionWithholdingRate DECIMAL(4,2)  NOT NULL CONSTRAINT DF_INV_Invoices_CommWRate DEFAULT 0,
    PreventCostUtility      BIT             NOT NULL CONSTRAINT DF_INV_Invoices_PreventCU DEFAULT 0,
    VoucherConsecutive      BIT             NOT NULL CONSTRAINT DF_INV_Invoices_VouchConsec DEFAULT 0,
    SingleDiscountOnly      BIT             NOT NULL CONSTRAINT DF_INV_Invoices_SingleDsc DEFAULT 0,
    VatWithDiscount         BIT             NOT NULL CONSTRAINT DF_INV_Invoices_VatWDsc DEFAULT 0,
    ThirdPartySpecialLineId NVARCHAR(5)     NULL,                       -- LincredTeresp
    ThirdPartyCreditLineId  NVARCHAR(5)     NULL,                       -- LincredTer
    ThirdPartySpecialVoucher NVARCHAR(5)    NULL,                       -- CpteCartTeresp
    ThirdPartyVoucherCode   NVARCHAR(5)     NULL,                       -- CpteCartTer
    -- Audit columns
    CreatedAt               DATETIME2       NOT NULL CONSTRAINT DF_INV_Invoices_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(50)    NULL,
    UpdatedAt               DATETIME2       NULL,
    UpdatedBy               NVARCHAR(50)    NULL,
    IsDeleted               BIT             NOT NULL CONSTRAINT DF_INV_Invoices_IsDeleted DEFAULT 0,
    DeletedAt               DATETIME2       NULL,
    DeletedBy               NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_Invoices PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_Invoices_PublicId      UNIQUE (PublicId),
    CONSTRAINT UK_INV_Invoices_InvoiceCode   UNIQUE (InvoiceCode)
);
GO

-- ============================================================
-- 217. INV_Documents  (orig: inv_docs)
-- ============================================================
CREATE TABLE [dbo].[INV_Documents] (
    Id                  BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_Documents_PublicId DEFAULT NEWID(),
    TransactionTypeId   INT             NOT NULL,                       -- IdTipoMovto
    SequenceNumber      DECIMAL(18,0)   NOT NULL,                       -- Secuencia
    CustomerId          INT             NULL,                           -- IdCliente
    EntryDate           DATETIME2       NOT NULL,                       -- FecIng
    TotalAmount         DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Documents_Total DEFAULT 0,
    DiscountAmount      DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Documents_Dscto DEFAULT 0,
    VatAmount           DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Documents_Vat DEFAULT 0,
    Status              INT             NOT NULL CONSTRAINT DF_INV_Documents_Status DEFAULT 0,
    UserId              NVARCHAR(20)    NULL,                           -- IdUsuario
    PaymentClassId      INT             NOT NULL CONSTRAINT DF_INV_Documents_PayCls DEFAULT 0,
    CashAmount          DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Documents_Cash DEFAULT 0,
    CreditAmount        DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Documents_Credit DEFAULT 0,
    DebitCardAmount     DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Documents_DebitCard DEFAULT 0,
    CreditCardAmount    DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Documents_CreditCard DEFAULT 0,
    CheckAmount         DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Documents_Check DEFAULT 0,
    BankId              NVARCHAR(10)    NULL,                           -- Idbanco
    ItemCount           INT             NOT NULL CONSTRAINT DF_INV_Documents_ItemCnt DEFAULT 0,
    BankAccountNumber   NVARCHAR(20)    NULL,                           -- CuentaBanco
    SalesPointId        INT             NULL,                           -- IdPunto
    ShiftId             INT             NULL,                           -- IdTurno
    AuditAmount         DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Documents_Audit DEFAULT 0,
    Detail              NVARCHAR(100)   NULL,                           -- Detalle
    InvoiceNumber       BIGINT          NULL,                           -- Factura
    DueDate             DATE            NULL,                           -- fecvence
    SalesPersonId       INT             NULL,                           -- idVendedor
    ReturnTypeId        INT             NULL,                           -- idtipomovdev
    ReturnSequence      DECIMAL(18,0)   NULL,                           -- secuenciadev
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_Documents_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_Documents_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_Documents PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_Documents_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_INV_Documents_TypeSeq  UNIQUE (TransactionTypeId, SequenceNumber)
);
GO

-- ============================================================
-- 218. INV_OrderDocuments  (orig: inv_docs_orden)
-- ============================================================
CREATE TABLE [dbo].[INV_OrderDocuments] (
    Id                  BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_OrderDocs_PublicId DEFAULT NEWID(),
    TransactionTypeId   INT             NOT NULL,                       -- IdTipoMovto
    SequenceNumber      DECIMAL(18,0)   NOT NULL,                       -- Secuencia
    CustomerId          INT             NULL,                           -- IdCliente
    EntryDate           DATETIME2       NOT NULL,                       -- FecIng
    TotalAmount         DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderDocs_Total DEFAULT 0,
    DiscountAmount      DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderDocs_Dscto DEFAULT 0,
    VatAmount           DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderDocs_Vat DEFAULT 0,
    SubTotalAmount      DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderDocs_SubTotal DEFAULT 0,
    Status              NVARCHAR(5)     NULL,                           -- Estado
    UserId              NVARCHAR(20)    NULL,                           -- IdUsuario
    PaymentClassId      INT             NOT NULL CONSTRAINT DF_INV_OrderDocs_PayCls DEFAULT 0,
    CashAmount          DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderDocs_Cash DEFAULT 0,
    CreditAmount        DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderDocs_Credit DEFAULT 0,
    DebitCardAmount     DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderDocs_DebitCard DEFAULT 0,
    CreditCardAmount    DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderDocs_CreditCard DEFAULT 0,
    CheckAmount         DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderDocs_Check DEFAULT 0,
    BankId              NVARCHAR(10)    NULL,                           -- Idbanco
    ItemCount           INT             NOT NULL CONSTRAINT DF_INV_OrderDocs_ItemCnt DEFAULT 0,
    BankAccountNumber   NVARCHAR(20)    NULL,                           -- CuentaBanco
    SalesPointId        INT             NULL,                           -- IdPunto
    ShiftId             INT             NULL,                           -- IdTurno
    AuditAmount         DECIMAL(18,3)   NOT NULL CONSTRAINT DF_INV_OrderDocs_Audit DEFAULT 0,
    Detail              NVARCHAR(300)   NULL,                           -- Detalle
    InvoiceNumber       BIGINT          NULL,                           -- Factura
    ChangeAmount        DECIMAL(17,0)   NOT NULL CONSTRAINT DF_INV_OrderDocs_Change DEFAULT 0,
    Periodicity         INT             NULL,                           -- periodicidad
    Term                INT             NULL,                           -- plazo
    DeductionType       INT             NULL,                           -- clades
    DiscountDate        DATETIME2       NULL,                           -- fecdsto
    InstallmentAmount   DECIMAL(17,2)   NOT NULL CONSTRAINT DF_INV_OrderDocs_Install DEFAULT 0,
    InterestRate        DECIMAL(18,0)   NOT NULL CONSTRAINT DF_INV_OrderDocs_Rate DEFAULT 0,
    DueDate             DATE            NULL,                           -- fecvence
    WithholdingAmount   DECIMAL(15,2)   NOT NULL CONSTRAINT DF_INV_OrderDocs_WHAmt DEFAULT 0,
    IcaAmount           DECIMAL(15,2)   NOT NULL CONSTRAINT DF_INV_OrderDocs_IcaAmt DEFAULT 0,
    SalesPersonId       INT             NULL,                           -- idVendedor
    ReturnTypeId        INT             NULL,                           -- idtipomovdev
    ReturnSequence      DECIMAL(18,0)   NULL,                           -- secuenciadev
    TransferTypeId      INT             NULL,                           -- IdTipoMovto_trans
    TransferSequence    DECIMAL(18,0)   NULL,                           -- Secuencia_trans
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_OrderDocs_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_OrderDocs_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_OrderDocuments PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_OrderDocuments_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_INV_OrderDocuments_TypeSeq  UNIQUE (TransactionTypeId, SequenceNumber)
);
GO

-- ============================================================
-- 219. INV_Prices  (orig: inv_precios)
-- ============================================================
CREATE TABLE [dbo].[INV_Prices] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_Prices_PublicId DEFAULT NEWID(),
    PriceListTypeId     INT             NOT NULL,                       -- IdTipoPrecio
    ProductId           INT             NOT NULL,                       -- IdProducto
    CustomerType        NVARCHAR(5)     NOT NULL,                       -- TipoCliente
    StartDate           DATETIME2       NULL,                           -- FecIni
    EndDate             DATETIME2       NULL,                           -- FecFin
    Price               DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_Prices_Price DEFAULT 0,
    PriceClass          INT             NULL,                           -- ClasePrecio
    Description         NVARCHAR(50)    NULL,                           -- Descripcion
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_Prices_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_Prices_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_Prices PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_Prices_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_INV_Prices_TypeProdCust UNIQUE (PriceListTypeId, ProductId, CustomerType)
);
GO

-- ============================================================
-- 220. INV_PriceListTypes  (orig: inv_tipolistas)
-- ============================================================
CREATE TABLE [dbo].[INV_PriceListTypes] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_PriceListTypes_PublicId DEFAULT NEWID(),
    TypeCode            INT             NOT NULL,                       -- IdTipoPrecio
    Name                NVARCHAR(100)   NOT NULL,                       -- Descripcion
    ShortName           NVARCHAR(50)    NULL,                           -- (ClasePrecio stored separately)
    PriceClass          INT             NULL,                           -- ClasePrecio
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_PriceListTypes_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_PriceListTypes_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_PriceListTypes PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_PriceListTypes_PublicId  UNIQUE (PublicId),
    CONSTRAINT UK_INV_PriceListTypes_TypeCode  UNIQUE (TypeCode)
);
GO

-- ============================================================
-- 221. INV_Discounts  (orig: inv_dstos)
-- ============================================================
CREATE TABLE [dbo].[INV_Discounts] (
    Id                  BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_Discounts_PublicId DEFAULT NEWID(),
    ProductId           INT             NULL,                           -- IdProducto
    DiscountTypeId      INT             NULL,                           -- IdTipoDsto
    DiscountClass       INT             NULL,                           -- ClaseDsto
    CustomerId          NVARCHAR(20)    NULL,                           -- IdCliente
    ProductClass        NVARCHAR(5)     NULL,                           -- ClaProducto
    CustomerType        NVARCHAR(5)     NULL,                           -- TipoCliente
    PaymentClassId      INT             NULL,                           -- ClaPago
    StartDate           DATETIME2       NULL,                           -- Fecini
    EndDate             DATETIME2       NULL,                           -- FecFin
    QuantityStart       INT             NOT NULL CONSTRAINT DF_INV_Discounts_QtyStart DEFAULT 0,
    QuantityEnd         INT             NOT NULL CONSTRAINT DF_INV_Discounts_QtyEnd DEFAULT 0,
    PurchasePeriod      INT             NULL,                           -- PerCompra
    PurchaseAmount      INT             NOT NULL CONSTRAINT DF_INV_Discounts_PurchAmt DEFAULT 0,
    DiscountRate        DECIMAL(6,3)    NOT NULL CONSTRAINT DF_INV_Discounts_Rate DEFAULT 0,
    PeriodCode          INT             NULL,                           -- Periodo
    GroupId             NVARCHAR(10)    NULL,                           -- IdGrupo
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_Discounts_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_Discounts_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_Discounts PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_Discounts_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 222. INV_DiscountTypes  (orig: inv_tipodstos)
-- ============================================================
CREATE TABLE [dbo].[INV_DiscountTypes] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_DiscountTypes_PublicId DEFAULT NEWID(),
    TypeCode            INT             NOT NULL,                       -- IdTipoDsto
    Name                NVARCHAR(100)   NOT NULL,                       -- Descripcion
    ShortName           NVARCHAR(50)    NULL,
    DiscountClass       INT             NULL,                           -- ClaseDsto
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_DiscountTypes_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_DiscountTypes_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_DiscountTypes PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_DiscountTypes_PublicId  UNIQUE (PublicId),
    CONSTRAINT UK_INV_DiscountTypes_TypeCode  UNIQUE (TypeCode)
);
GO

-- ============================================================
-- 223. INV_Warehouses  (orig: inv_bodegas)
-- ============================================================
CREATE TABLE [dbo].[INV_Warehouses] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_Warehouses_PublicId DEFAULT NEWID(),
    WarehouseCode       INT             NOT NULL,                       -- IdBodega
    LocationId          INT             NOT NULL,                       -- idubicacion
    Description         NVARCHAR(100)   NOT NULL,                       -- Descripcion
    ShortDescription    NVARCHAR(50)    NULL,                           -- Resumido
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_Warehouses_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_Warehouses_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_Warehouses PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_Warehouses_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_INV_Warehouses_CodeLoc  UNIQUE (WarehouseCode, LocationId)
);
GO

-- ============================================================
-- 224. INV_Locations  (orig: inv_ubicacion)
-- ============================================================
CREATE TABLE [dbo].[INV_Locations] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_Locations_PublicId DEFAULT NEWID(),
    LocationCode        INT             NOT NULL,                       -- idubicacion
    Description         NVARCHAR(100)   NOT NULL,                       -- descripcion
    ShortDescription    NVARCHAR(50)    NULL,                           -- resumido
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_Locations_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_Locations_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_Locations PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_Locations_PublicId      UNIQUE (PublicId),
    CONSTRAINT UK_INV_Locations_LocationCode  UNIQUE (LocationCode)
);
GO

-- ============================================================
-- 225. INV_SalesPoints  (orig: inv_puntos)
-- ============================================================
CREATE TABLE [dbo].[INV_SalesPoints] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_SalesPoints_PublicId DEFAULT NEWID(),
    PointCode           INT             NOT NULL,                       -- IdPunto
    UserId              NVARCHAR(20)    NULL,                           -- IdUsuario
    TransactionTypeId   INT             NULL,                           -- IdTipoMovto
    PrinterName         NVARCHAR(50)    NULL,                           -- Printer
    Status              INT             NOT NULL CONSTRAINT DF_INV_SalesPoints_Status DEFAULT 0,
    DateId              DATETIME2       NULL,                           -- IdFecha
    ShiftId             INT             NULL,                           -- IdTurno
    BaseAmount          DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_SalesPoints_Base DEFAULT 0,
    WarehouseId         INT             NULL,                           -- idbodega
    LocationId          INT             NULL,                           -- idubicacion
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_SalesPoints_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_SalesPoints_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_SalesPoints PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_SalesPoints_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_INV_SalesPoints_PtStatDate UNIQUE (PointCode, Status, DateId)
);
GO

-- ============================================================
-- 226. INV_Shifts  (orig: inv_turnos)
-- ============================================================
CREATE TABLE [dbo].[INV_Shifts] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_Shifts_PublicId DEFAULT NEWID(),
    ShiftCode           INT             NOT NULL,                       -- IdTurno
    Name                NVARCHAR(50)    NOT NULL,                       -- Descripcion
    StartTime           NVARCHAR(10)    NULL,                           -- HoraInicial
    EndTime             NVARCHAR(10)    NULL,                           -- HoraFinal
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_Shifts_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_Shifts_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_Shifts PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_Shifts_PublicId   UNIQUE (PublicId),
    CONSTRAINT UK_INV_Shifts_ShiftCode  UNIQUE (ShiftCode)
);
GO

-- ============================================================
-- 227. INV_ProductAccounts  (orig: inv_cuentas)
-- ============================================================
CREATE TABLE [dbo].[INV_ProductAccounts] (
    Id                      INT             IDENTITY(1,1) NOT NULL,
    PublicId                UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_ProductAccounts_PublicId DEFAULT NEWID(),
    ProductGroupId          INT             NOT NULL,                   -- IdGruProducto
    TransactionTypeId       INT             NOT NULL,                   -- IdTipoMovto
    WarehouseId             INT             NOT NULL,                   -- idbodega
    LocationId              INT             NOT NULL,                   -- idubicacion
    VatAccountCode          NVARCHAR(15)    NULL,                       -- Iva
    DiscountAccountCode     NVARCHAR(15)    NULL,                       -- Dstos
    TaxableSalesAccountCode NVARCHAR(15)    NULL,                       -- VentasGrabadas
    NonTaxableSalesAccountCode NVARCHAR(15) NULL,                       -- VentasNoGrabadas
    NetAccountCode          NVARCHAR(15)    NULL,                       -- Neto
    -- Audit columns
    CreatedAt               DATETIME2       NOT NULL CONSTRAINT DF_INV_ProductAccounts_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy               NVARCHAR(50)    NULL,
    UpdatedAt               DATETIME2       NULL,
    UpdatedBy               NVARCHAR(50)    NULL,
    IsDeleted               BIT             NOT NULL CONSTRAINT DF_INV_ProductAccounts_IsDeleted DEFAULT 0,
    DeletedAt               DATETIME2       NULL,
    DeletedBy               NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_ProductAccounts PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_ProductAccounts_PublicId UNIQUE (PublicId),
    CONSTRAINT UK_INV_ProductAccounts_Composite UNIQUE (ProductGroupId, TransactionTypeId, WarehouseId, LocationId)
);
GO

-- ============================================================
-- 228. INV_VatAccounts  (orig: inv_cuentasiva)
-- ============================================================
CREATE TABLE [dbo].[INV_VatAccounts] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_VatAccounts_PublicId DEFAULT NEWID(),
    ProductGroupId      INT             NOT NULL,                       -- IdGruProducto
    TransactionTypeId   INT             NOT NULL,                       -- IdTipoMovto
    WarehouseId         INT             NOT NULL,                       -- idbodega
    LocationId          INT             NOT NULL,                       -- idubicacion
    VatRate             DECIMAL(6,3)    NOT NULL,                       -- tasa
    AccountType         NVARCHAR(5)     NULL,                           -- tipo
    AccountCode         NVARCHAR(15)    NULL,                           -- cuenta
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_VatAccounts_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_VatAccounts_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_VatAccounts PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_VatAccounts_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 229. INV_PhysicalInventory  (orig: inv_invfisico)
-- ============================================================
CREATE TABLE [dbo].[INV_PhysicalInventory] (
    Id                  BIGINT          IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_PhysicalInventory_PublicId DEFAULT NEWID(),
    PeriodCode          NVARCHAR(10)    NOT NULL,                       -- Periodo
    ProductId           INT             NOT NULL,                       -- Idproducto
    LocationId          INT             NOT NULL,                       -- idubicacion
    WarehouseId         INT             NOT NULL,                       -- idbodega
    PhysicalCount       INT             NOT NULL CONSTRAINT DF_INV_PhysInv_Physical DEFAULT 0,
    TheoreticalCount    INT             NOT NULL CONSTRAINT DF_INV_PhysInv_Theoretical DEFAULT 0,
    Cost                DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_PhysInv_Cost DEFAULT 0,
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_PhysicalInventory_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_PhysicalInventory_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_PhysicalInventory PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_PhysicalInventory_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 230. INV_CommissionParameters  (orig: inv_param_comisiones)
-- ============================================================
CREATE TABLE [dbo].[INV_CommissionParameters] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_CommissionParams_PublicId DEFAULT NEWID(),
    InvoiceTypeId       INT             NOT NULL,                       -- idFactura
    CommissionGroupId   INT             NOT NULL,                       -- idGrupoComision
    GroupId             INT             NOT NULL,                       -- idGrupo
    SalesRangeStart     DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_CommParams_Start DEFAULT 0,
    SalesRangeEnd       DECIMAL(18,2)   NOT NULL CONSTRAINT DF_INV_CommParams_End DEFAULT 0,
    CommissionRate      DECIMAL(6,3)    NOT NULL CONSTRAINT DF_INV_CommParams_Rate DEFAULT 0,
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_CommissionParams_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_CommissionParams_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_CommissionParameters PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_CommissionParams_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 231. INV_CommissionPriceParams  (orig: inv_param_comisiones_Precios)
-- ============================================================
CREATE TABLE [dbo].[INV_CommissionPriceParams] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_CommPriceParams_PublicId DEFAULT NEWID(),
    InvoiceTypeId       INT             NOT NULL,                       -- idFactura
    CommissionGroupId   INT             NOT NULL,                       -- idGrupoComision
    GroupId             INT             NOT NULL,                       -- idGrupo
    CustomerType        NVARCHAR(5)     NOT NULL,                       -- TipoCliente
    CommissionRate      DECIMAL(6,3)    NOT NULL CONSTRAINT DF_INV_CommPriceParams_Rate DEFAULT 0,
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_CommPriceParams_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_CommPriceParams_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_CommissionPriceParams PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_CommPriceParams_PublicId UNIQUE (PublicId)
);
GO

-- ============================================================
-- 232. INV_Salespeople  (orig: inv_Vendedor)
-- ============================================================
CREATE TABLE [dbo].[INV_Salespeople] (
    Id                  INT             IDENTITY(1,1) NOT NULL,
    PublicId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INV_Salespeople_PublicId DEFAULT NEWID(),
    IdNumber            NVARCHAR(20)    NOT NULL,                       -- cedula
    Name                NVARCHAR(100)   NOT NULL,                       -- nombre
    LastName            NVARCHAR(100)   NULL,                           -- apellido
    Address             NVARCHAR(100)   NULL,                           -- direccion
    Phone               NVARCHAR(30)    NULL,                           -- telefono
    Mobile              NVARCHAR(30)    NULL,                           -- celular
    CityId              INT             NULL,                           -- ciudad
    SalespersonType     INT             NULL,                           -- tipo_vendedor
    AppliesCommission   BIT             NOT NULL CONSTRAINT DF_INV_Salespeople_Commission DEFAULT 0,
    -- Audit columns
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_INV_Salespeople_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy           NVARCHAR(50)    NULL,
    UpdatedAt           DATETIME2       NULL,
    UpdatedBy           NVARCHAR(50)    NULL,
    IsDeleted           BIT             NOT NULL CONSTRAINT DF_INV_Salespeople_IsDeleted DEFAULT 0,
    DeletedAt           DATETIME2       NULL,
    DeletedBy           NVARCHAR(50)    NULL,
    CONSTRAINT PK_INV_Salespeople PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UK_INV_Salespeople_PublicId  UNIQUE (PublicId),
    CONSTRAINT UK_INV_Salespeople_IdNumber  UNIQUE (IdNumber)
);
GO

-- ============================================================
-- END OF FILE: INV_ Module — 24 tables
-- ============================================================
