using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Approvals.Transactions;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Entities.Audit;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Debit;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Inventory;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury;
using IngenIA365ERP.Domain.Entities.Web;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Core
    DbSet<Person> People { get; }
    DbSet<Associate> Associates { get; }
    DbSet<Branch> Branches { get; }
    DbSet<CostCenter> CostCenters { get; }
    DbSet<AssociateGroup> AssociateGroups { get; }
    DbSet<City> Cities { get; }
    DbSet<Bank> Banks { get; }
    DbSet<BankFileFormat> BankFileFormats { get; }
    DbSet<BankFileFormatField> BankFileFormatFields { get; }
    DbSet<Company> Companies { get; }
    DbSet<Committee> Committees { get; }
    DbSet<Beneficiary> Beneficiaries { get; }
    DbSet<Reference> References { get; }
    DbSet<Country> Countries { get; }
    DbSet<Department> Departments { get; }
    DbSet<Section> Sections { get; }
    DbSet<Profession> Professions { get; }
    DbSet<Position> Positions { get; }
    DbSet<Relationship> Relationships { get; }
    DbSet<WithdrawalReason> WithdrawalReasons { get; }
    DbSet<Sport> Sports { get; }
    DbSet<CulturalActivity> CulturalActivities { get; }
    DbSet<Disease> Diseases { get; }
    DbSet<ExternalEntity> ExternalEntities { get; }
    DbSet<Agreement> Agreements { get; }
    DbSet<Course> Courses { get; }
    DbSet<Spouse> Spouses { get; }
    DbSet<PersonFinancial> PeopleFinancial { get; }
    DbSet<AssociateCategory> AssociateCategories { get; }
    DbSet<CommitteeMember> CommitteeMembers { get; }
    DbSet<EmployerCompany> EmployerCompanies { get; }
    DbSet<Advisor> Advisors { get; }
    DbSet<PaymentMethod> PaymentMethods { get; }
    DbSet<Attachment> Attachments { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationDeliveryFailure> NotificationDeliveryFailures { get; }

    // Compliance — Habeas data (US7)
    DbSet<HabeasDataPolicyVersion> HabeasDataPolicyVersions { get; }
    DbSet<HabeasDataConsent> HabeasDataConsents { get; }

    // Accounting (feature 009): modelo nuevo; las 33 tablas heredadas se retiraron.
    // Documentos y lineas viven en Entities.Accounting.Transactions (Principio XI).
    DbSet<AccountingSetup> AccountingSetups { get; }
    DbSet<AccountCatalog> AccountCatalogs { get; }
    DbSet<AccountCatalogEntry> AccountCatalogEntries { get; }
    DbSet<FinancialStatementItem> FinancialStatementItems { get; }
    DbSet<ChartOfAccount> ChartOfAccounts { get; }
    DbSet<AccountTaxRate> AccountTaxRates { get; }
    DbSet<VoucherType> VoucherTypes { get; }
    DbSet<CrossDocumentType> CrossDocumentTypes { get; }
    DbSet<FiscalYear> FiscalYears { get; }
    DbSet<AccountingPeriod> AccountingPeriods { get; }
    DbSet<AccountingDocument> AccountingDocuments { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<BankStatementColumnMap> BankStatementColumnMaps { get; }
    DbSet<BankReconciliation> BankReconciliations { get; }
    DbSet<BankStatementLine> BankStatementLines { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<BudgetLine> BudgetLines { get; }
    DbSet<WithholdingCertificate> WithholdingCertificates { get; }
    DbSet<WithholdingCertificateLine> WithholdingCertificateLines { get; }
    DbSet<TaxForm> TaxForms { get; }
    DbSet<TaxFormLine> TaxFormLines { get; }
    DbSet<ExogenousFormat> ExogenousFormats { get; }
    DbSet<ExogenousConcept> ExogenousConcepts { get; }
    DbSet<ExogenousConceptAccount> ExogenousConceptAccounts { get; }
    DbSet<ExogenousRun> ExogenousRuns { get; }
    DbSet<ExogenousRunLine> ExogenousRunLines { get; }
    DbSet<FixedAsset> FixedAssets { get; }
    DbSet<FixedAssetInstallment> FixedAssetInstallments { get; }
    DbSet<AssetRun> AssetRuns { get; }

    // Payroll
    DbSet<Employee> Employees { get; }
    DbSet<PayrollConcept> PayrollConcepts { get; }
    DbSet<PayPeriod> PayPeriods { get; }
    DbSet<HealthInsuranceProvider> HealthInsuranceProviders { get; }
    DbSet<WorkRiskProvider> WorkRiskProviders { get; }
    DbSet<WorkRiskRate> WorkRiskRates { get; }
    DbSet<PensionProvider> PensionProviders { get; }
    DbSet<SeveranceProvider> SeveranceProviders { get; }
    DbSet<FamilyCompensationFund> FamilyCompensationFunds { get; }
    DbSet<ConceptAccount> ConceptAccounts { get; }
    DbSet<WithholdingParameter> WithholdingParameters { get; }
    DbSet<WithholdingCause> WithholdingCauses { get; }
    DbSet<AutoContributionParam> AutoContributionParams { get; }
    DbSet<PayrollTransaction> PayrollTransactions { get; }
    DbSet<PayrollEntry> PayrollEntries { get; }
    DbSet<SalaryChange> SalaryChanges { get; }
    DbSet<Absence> Absences { get; }
    DbSet<TaxCertificate> TaxCertificates { get; }

    // Payroll — feature 005 (novedades y liquidacion)
    DbSet<PayrollPlan> PayrollPlans { get; }
    DbSet<PayrollConceptDefinition> PayrollConceptDefinitions { get; }
    DbSet<PayrollConceptDefinitionAccount> PayrollConceptDefinitionAccounts { get; }
    DbSet<PayrollLegalParameter> PayrollLegalParameters { get; }
    DbSet<PayrollLegalParameterRange> PayrollLegalParameterRanges { get; }
    DbSet<PayrollNovelty> PayrollNovelties { get; }
    DbSet<PayrollRecurringNovelty> PayrollRecurringNovelties { get; }
    DbSet<EmployeeWithholdingRate> EmployeeWithholdingRates { get; }
    DbSet<EmployeeTaxDeduction> EmployeeTaxDeductions { get; }
    DbSet<Domain.Entities.Payroll.Transactions.PayrollRun> PayrollRuns { get; }
    DbSet<Domain.Entities.Payroll.Transactions.PayrollRunEmployee> PayrollRunEmployees { get; }
    DbSet<Domain.Entities.Payroll.Transactions.PayrollRunLine> PayrollRunLines { get; }
    DbSet<PayrollPayment> PayrollPayments { get; }
    DbSet<PayslipDelivery> PayslipDeliveries { get; }

    // Payroll — feature 010 (prestaciones, retiro, procedimiento 2; entrega N1)
    DbSet<CompanyPolicy> CompanyPolicies { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<EmployeeBenefitOpeningBalance> EmployeeBenefitOpeningBalances { get; }
    DbSet<VacationMovement> VacationMovements { get; }
    DbSet<TerminationReason> TerminationReasons { get; }
    DbSet<EmploymentTermination> EmploymentTerminations { get; }
    DbSet<SettlementDeduction> SettlementDeductions { get; }

    /// <summary>Feature 013 (T0155): descuentos de Cartera de la nómina ordinaria por empleado y obligación.</summary>
    DbSet<Domain.Entities.Payroll.Transactions.PayrollLoanDeduction> PayrollLoanDeductions { get; }
    DbSet<Domain.Entities.Payroll.Transactions.PayrollAccountDeduction> PayrollAccountDeductions { get; }
    DbSet<WithholdingRateCalculation> WithholdingRateCalculations { get; }
    DbSet<WithholdingRateCalculationMonth> WithholdingRateCalculationMonths { get; }
    DbSet<SeveranceFundDeposit> SeveranceFundDeposits { get; }
    DbSet<Domain.Entities.Payroll.Transactions.BankDisbursementFile> BankDisbursementFiles { get; }
    DbSet<Domain.Entities.Payroll.Transactions.BankDisbursementFileLine> BankDisbursementFileLines { get; }
    DbSet<Domain.Entities.Payroll.Pila.PilaSettings> PilaSettings { get; }
    DbSet<Domain.Entities.Payroll.Transactions.PilaGeneration> PilaGenerations { get; }
    DbSet<Domain.Entities.Payroll.Transactions.PilaGenerationLine> PilaGenerationLines { get; }
    DbSet<Domain.Entities.Payroll.Pila.PilaIssue> PilaIssues { get; }
    DbSet<Domain.Entities.Payroll.ElectronicPayroll.ElectronicPayrollSettings> ElectronicPayrollSettings { get; }
    DbSet<Domain.Entities.Payroll.ElectronicPayroll.ElectronicPayrollNumberingRange> ElectronicPayrollNumberingRanges { get; }
    DbSet<Domain.Entities.Payroll.Transactions.ElectronicPayrollDocument> ElectronicPayrollDocuments { get; }
    DbSet<Domain.Entities.Payroll.Transactions.ElectronicPayrollTransmission> ElectronicPayrollTransmissions { get; }

    // Inventory
    DbSet<Salesperson> Salespeople { get; }
    // Feature 012 (T17, T136): documento generico de inventario. Number y NextValue los escribe solo Numerador;
    // DocumentPartySnapshots y DocumentTaxLines son hechos (solo insercion).
    DbSet<InventoryDocument> InventoryDocuments { get; }
    DbSet<InventoryDocumentLine> InventoryDocumentLines { get; }
    DbSet<DocumentLink> DocumentLinks { get; }
    DbSet<DocumentLineLink> DocumentLineLinks { get; }
    DbSet<DocumentPartySnapshot> DocumentPartySnapshots { get; }
    DbSet<DocumentTaxLine> DocumentTaxLines { get; }
    DbSet<InventoryDocumentType> InventoryDocumentTypes { get; }
    DbSet<DocumentTypeWarehouse> DocumentTypeWarehouses { get; }
    DbSet<DocumentSequence> DocumentSequences { get; }
    // Feature 012 (T209, US1): catalogo y bodegas. ProductAccountingGroupChanges es un hecho (solo insercion; lo escribe
    // ChangeProductAccountingGroupCommand, US3). INV_UserWarehouseScopes se lee y escribe solo por IAsignacionesDeBodega.
    DbSet<Domain.Entities.Inventory.Catalog.UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<Domain.Entities.Inventory.Catalog.ProductCategory> ProductCategories { get; }
    DbSet<Domain.Entities.Inventory.Catalog.Brand> Brands { get; }
    DbSet<Domain.Entities.Inventory.Catalog.AccountingGroup> AccountingGroups { get; }
    DbSet<Domain.Entities.Inventory.Catalog.SalesChannel> SalesChannels { get; }
    DbSet<Domain.Entities.Inventory.Catalog.Product> Products { get; }
    DbSet<Domain.Entities.Inventory.Catalog.ProductUnit> ProductUnits { get; }
    DbSet<Domain.Entities.Inventory.Catalog.ProductBarcode> ProductBarcodes { get; }
    DbSet<Domain.Entities.Inventory.Catalog.ProductTax> ProductTaxes { get; }
    DbSet<Domain.Entities.Inventory.Catalog.ProductAccountingGroupChange> ProductAccountingGroupChanges { get; }
    DbSet<Domain.Entities.Inventory.Warehousing.WarehouseType> WarehouseTypes { get; }
    DbSet<Domain.Entities.Inventory.Warehousing.Warehouse> Warehouses { get; }
    DbSet<Domain.Entities.Inventory.Warehousing.WarehouseLocation> WarehouseLocations { get; }
    DbSet<Domain.Entities.Inventory.Warehousing.ReorderPolicy> ReorderPolicies { get; }
    DbSet<Domain.Entities.Inventory.Documents.AdjustmentCause> AdjustmentCauses { get; }
    DbSet<Domain.Entities.Inventory.Security.UserWarehouseScope> UserWarehouseScopes { get; }
    // Feature 012 (T250, US2): el kardex y sus proyecciones. Los escriben solo RegistroDeKardex y
    // RebuildInventoryProjectionsCommand (NadieEscribeElKardexFueraDelRegistro).
    DbSet<Domain.Entities.Inventory.Transactions.KardexEntry> KardexEntries { get; }
    DbSet<Domain.Entities.Inventory.Projections.StockBalance> StockBalances { get; }
    DbSet<Domain.Entities.Inventory.Projections.StockDetail> StockDetails { get; }
    DbSet<Domain.Entities.Inventory.Projections.CostState> CostStates { get; }
    // Feature 012 (T284, US3): puesta en marcha, períodos y valorizado fijado al cerrar.
    DbSet<Domain.Entities.Inventory.Periods.InventorySetup> InventorySetups { get; }
    DbSet<Domain.Entities.Inventory.Periods.InventoryPeriod> InventoryPeriods { get; }
    DbSet<Domain.Entities.Inventory.Periods.PeriodClosingBalance> PeriodClosingBalances { get; }
    // Feature 012 (T307, US4): la activación de cada bodega y las cifras de referencia (sólo informativas: nunca kardex).
    DbSet<Domain.Entities.Inventory.GoLive.WarehouseActivation> WarehouseActivations { get; }
    DbSet<Domain.Entities.Inventory.GoLive.LegacyFigure> LegacyFigures { get; }
    // Feature 012, US9 (T335-T337): el documento del proveedor y sus eventos RADIAN.
    DbSet<Domain.Entities.Inventory.Purchasing.SupplierInvoiceDetail> SupplierInvoiceDetails { get; }
    DbSet<Domain.Entities.Inventory.Purchasing.SupplierInvoiceEvent> SupplierInvoiceEvents { get; }
    // Feature 012 (US10, T366): faltantes y sobrantes de los traslados.
    DbSet<Domain.Entities.Inventory.Documents.TransferDiscrepancy> TransferDiscrepancies { get; }
    // US11 (T391): la foto y las capturas de los conteos físicos (INV_CountSnapshotLines, INV_CountCaptures).
    DbSet<Domain.Entities.Inventory.Documents.CountSnapshotLine> CountSnapshotLines { get; }
    DbSet<Domain.Entities.Inventory.Documents.CountCapture> CountCaptures { get; }
    // Feature 012, I5 (T835): el cruce a tres vías, el reparto de los costos adicionales y las capas PEPS con sus consumos.
    // Las capas y los consumos los escriben sólo RegistroDeKardex y RebuildInventoryProjectionsCommand.
    DbSet<Domain.Entities.Inventory.Purchasing.PurchaseMatchLine> PurchaseMatchLines { get; }
    DbSet<Domain.Entities.Inventory.Purchasing.LandedCostAllocation> LandedCostAllocations { get; }
    DbSet<Domain.Entities.Inventory.Projections.CostLayer> CostLayers { get; }
    DbSet<Domain.Entities.Inventory.Transactions.LayerConsumption> LayerConsumptions { get; }
    // Feature 012, I6 (T859; ComercioAmpliado): catálogo avanzado, lotes y series, reservas y promociones. La proyección de la serie
    // (InStock*) la escribe sólo RegistroDeKardex.
    DbSet<Domain.Entities.Inventory.Catalog.ProductComponent> ProductComponents { get; }
    DbSet<Domain.Entities.Inventory.Catalog.VariantAttribute> VariantAttributes { get; }
    DbSet<Domain.Entities.Inventory.Catalog.VariantAttributeValue> VariantAttributeValues { get; }
    DbSet<Domain.Entities.Inventory.Catalog.ProductVariantValue> ProductVariantValues { get; }
    DbSet<Domain.Entities.Inventory.Catalog.Lot> Lots { get; }
    DbSet<Domain.Entities.Inventory.Catalog.Serial> Serials { get; }
    DbSet<Domain.Entities.Inventory.Warehousing.Reservation> Reservations { get; }
    DbSet<Domain.Entities.Inventory.Pricing.Promotion> Promotions { get; }
    DbSet<Domain.Entities.Inventory.Pricing.PromotionScope> PromotionScopes { get; }
    DbSet<Domain.Entities.Inventory.Pricing.PromotionTier> PromotionTiers { get; }
    // Feature 012 (T585, I3): medios de pago de Core, punto de venta y caja, precios y satélites de la venta. INV_UserPointOfSaleScopes
    // se lee y escribe solo por IAsignacionesDePuntoDeVenta; las redenciones de bonos, sólo al confirmar la venta.
    DbSet<Domain.Entities.Core.Payments.PaymentMeans> PaymentMeans { get; }
    DbSet<Domain.Entities.Core.Payments.CardNetwork> CardNetworks { get; }
    DbSet<Domain.Entities.Core.Payments.CardAcquirer> CardAcquirers { get; }
    DbSet<Domain.Entities.Core.Payments.CardTerminal> CardTerminals { get; }
    DbSet<Domain.Entities.Core.Payments.CashDenomination> CashDenominations { get; }
    DbSet<Domain.Entities.Inventory.Pos.PointOfSale> PointsOfSale { get; }
    DbSet<Domain.Entities.Inventory.Pos.CashRegister> CashRegisters { get; }
    DbSet<Domain.Entities.Inventory.Pos.CashRegisterDocumentType> CashRegisterDocumentTypes { get; }
    DbSet<Domain.Entities.Inventory.Pos.PaymentMeansPointOfSale> PaymentMeansPointsOfSale { get; }
    DbSet<Domain.Entities.Inventory.Pos.PaymentMeansChannel> PaymentMeansChannels { get; }
    DbSet<Domain.Entities.Inventory.Pos.PaymentMeansDocumentType> PaymentMeansDocumentTypes { get; }
    DbSet<Domain.Entities.Inventory.Pos.CashSession> CashSessions { get; }
    DbSet<Domain.Entities.Inventory.Pos.CashMovementDetail> CashMovementDetails { get; }
    DbSet<Domain.Entities.Inventory.Pos.CashCount> CashCounts { get; }
    DbSet<Domain.Entities.Inventory.Pos.CashCountLine> CashCountLines { get; }
    DbSet<Domain.Entities.Inventory.Pos.CashCountDenomination> CashCountDenominations { get; }
    DbSet<Domain.Entities.Inventory.Pos.CashCountTerminalBatch> CashCountTerminalBatches { get; }
    DbSet<Domain.Entities.Inventory.Pos.CashCountReferenceCheck> CashCountReferenceChecks { get; }
    DbSet<Domain.Entities.Inventory.Pos.CashDocumentLine> CashDocumentLines { get; }
    DbSet<Domain.Entities.Inventory.Pos.DayClose> DayCloses { get; }
    DbSet<Domain.Entities.Inventory.Pos.DayCloseLine> DayCloseLines { get; }
    DbSet<Domain.Entities.Inventory.Documents.DocumentPayment> DocumentPayments { get; }

    /// <summary>Feature 013 (T0174): recibo de las respuestas de Cartera a una venta a crédito (hecho; lo escribe sólo <c>DestinoInventario</c>).</summary>
    DbSet<Domain.Entities.Inventory.Documents.LendingObligationResponse> LendingObligationResponses { get; }
    DbSet<Domain.Entities.Inventory.Documents.VoucherRedemption> VoucherRedemptions { get; }
    DbSet<Domain.Entities.Inventory.Documents.DocumentLineDiscount> DocumentLineDiscounts { get; }
    DbSet<Domain.Entities.Inventory.Pricing.PriceList> PriceLists { get; }
    DbSet<Domain.Entities.Inventory.Pricing.PriceListItem> PriceListItems { get; }
    DbSet<Domain.Entities.Inventory.Pricing.DiscountCap> DiscountCaps { get; }
    DbSet<Domain.Entities.Inventory.Security.UserPointOfSaleScope> UserPointOfSaleScopes { get; }

    // Feature 012 (T698, I4): facturación electrónica DIAN (COR_Electronic*, COR_Dian*).
    DbSet<Domain.Entities.ElectronicInvoicing.ElectronicEmissionSetting> ElectronicEmissionSettings { get; }
    DbSet<Domain.Entities.ElectronicInvoicing.DianNumberingResolution> DianNumberingResolutions { get; }
    DbSet<Domain.Entities.ElectronicInvoicing.DianResolutionChannel> DianResolutionChannels { get; }
    DbSet<Domain.Entities.ElectronicInvoicing.DianContingencyEvent> DianContingencyEvents { get; }
    DbSet<Domain.Entities.ElectronicInvoicing.ElectronicDocument> ElectronicDocuments { get; }
    DbSet<Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentVersion> ElectronicDocumentVersions { get; }
    DbSet<Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentTransmission> ElectronicDocumentTransmissions { get; }

    // Feature 012 (T22, T161): catalogo tributario de Core. Lo escriben solo los comandos de Core/Taxes (y su
    // plantilla y semilla); lo lee para el motor solo LectorDeCatalogoTributario.
    DbSet<Domain.Entities.Core.Taxes.TaxDefinition> TaxDefinitions { get; }
    DbSet<Domain.Entities.Core.Taxes.TaxRate> TaxRates { get; }
    DbSet<Domain.Entities.Core.Taxes.WithholdingConcept> WithholdingConcepts { get; }

    // Debit
    DbSet<DebitCard> DebitCards { get; }
    DbSet<DebitAgreementParameter> DebitAgreementParameters { get; }
    DbSet<PosTerminal> PosTerminals { get; }
    DbSet<DebitDailyParameter> DebitDailyParameters { get; }
    DbSet<DebitTransaction> DebitTransactions { get; }
    DbSet<DebitAgreement> DebitAgreements { get; }

    // Treasury
    DbSet<Check> Checks { get; }
    DbSet<TreasuryConcept> TreasuryConcepts { get; }
    DbSet<TreasuryInvoice> TreasuryInvoices { get; }

    // Security
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }

    // Parametros de configuracion de la cooperativa (COR_SystemSettings).
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<LoginAttempt> LoginAttempts { get; }
    DbSet<UserTenantAssignment> UserTenantAssignments { get; }
    DbSet<UserBranchAssignment> UserBranchAssignments { get; }
    DbSet<PasswordPolicy> PasswordPolicies { get; }
    DbSet<PasswordHistory> PasswordHistory { get; }
    DbSet<MfaBackupCode> MfaBackupCodes { get; }
    DbSet<MfaResetRequest> MfaResetRequests { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }

    // Feature 012 (T13, T054): claves de idempotencia de las operaciones de pantalla. Las lee y escribe
    // solo IdempotencyBehavior. (Adelanto de T096, que registra el resto de DbSet de la plataforma.)
    DbSet<OperationKey> OperationKeys { get; }

    // Feature 012 (T10, T047; T096): arrendamientos de los trabajos de fondo, uno por nombre y por cooperativa. Los
    // toma, renueva y suelta solo ArrendamientosEnBase (IArrendamientos); las filas las siembra la migracion.
    DbSet<BackgroundLease> BackgroundLeases { get; }

    // Feature 012 (T37, T38; T061–T066): auditoria garantizada de los modulos encadenados. Escriben
    // AuditableEntityInterceptor, AuditBehavior y AuditoriaEncadenada; sella y reenvia solo el
    // AuditOutboxForwarder. (Adelanto de T096.)
    DbSet<AuditOutboxEntry> AuditOutbox { get; }
    DbSet<AuditChainHead> AuditChainHeads { get; }
    DbSet<AuditAnchor> AuditAnchors { get; }

    // Feature 012 (T21, T069-T071): parametros con vigencia. Los leen solo LectorDeParametros y los escribe solo
    // AddParameterVersionCommandHandler (LosParametrosSeLeenEnUnSoloSitio). (Adelanto de T096.)
    DbSet<ParameterVersion> ParameterVersions { get; }

    // Feature 012 (T7, T9; T073-T078): bandeja de salida de mensajes de integracion. Escribe mensajes, entregas y
    // dependencias solo EmisorDeMensajes (dentro del SaveChanges del documento); las entregas las actualizan
    // despues los comandos de I2. (Adelanto de T096.)
    DbSet<IntegrationMessage> IntegrationMessages { get; }
    DbSet<IntegrationMessageDependency> IntegrationMessageDependencies { get; }
    DbSet<IntegrationMessageDelivery> IntegrationMessageDeliveries { get; }
    // Feature 012, entrega I2 (T483): intentos, lotes y su consecutivo (plataforma), y el lado contable de la
    // integración con Inventario (matriz, mapeo de tipos de comprobante y recibos). Migración IntegracionContableDeInventario.
    DbSet<IntegrationDeliveryAttempt> IntegrationDeliveryAttempts { get; }
    DbSet<IntegrationBatch> IntegrationBatches { get; }
    DbSet<IntegrationBatchCounter> IntegrationBatchCounters { get; }
    DbSet<InventoryPostingRule> InventoryPostingRules { get; }
    DbSet<InventoryVoucherMapping> InventoryVoucherMappings { get; }
    DbSet<InventoryPosting> InventoryPostings { get; }
    // Feature 013, US7 (T0636; data-model parte 3 §5): el lado contable de la integración con Cartera (matriz, mapeo de tipos de
    // comprobante, recibos e indicadores de la entidad). Migración IntegracionContableDeCartera. Cartera no los nombra (T51).
    DbSet<IngenIA365ERP.Domain.Entities.Accounting.Lending.LendingPostingRule> LendingPostingRules { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Accounting.Lending.LendingVoucherMapping> LendingVoucherMappings { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Accounting.Transactions.LendingPosting> LendingPostings { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Accounting.Lending.RegulatoryIndicator> RegulatoryIndicators { get; }

    // Feature 012 (T33, T34; T081-T085): aprobaciones de plataforma y montos maximos por permiso. Escriben solo
    // MotorDeAprobaciones (solicitudes y decisiones), SaveApprovalPolicyCommand y SetPermissionAmountLimitCommand;
    // lee los limites ILimitesPorPermiso. (Adelanto de T096.)
    DbSet<ApprovalPolicy> ApprovalPolicies { get; }
    DbSet<ApprovalPolicyLevel> ApprovalPolicyLevels { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<ApprovalDecision> ApprovalDecisions { get; }
    DbSet<PermissionAmountLimit> PermissionAmountLimits { get; }

    // Feature 012 (T39; T091-T094): alertas de plataforma. Escriben solo IAlertas (levantar, atender por proceso),
    // AttendAlertCommand, SaveAlertTypeCommand y AlertTypesSeeder. (Adelanto de T096.)
    DbSet<AlertType> AlertTypes { get; }
    DbSet<Alert> Alerts { get; }

    // Feature 013 (T37, T39, T41; data-model parte 3 §4.1–§4.4): la copia de los valores nacionales. Escribe las copias y la
    // bitácora sólo ReplicacionDeValoresNacionales; las leen sólo IValoresNacionales, ICalendarioHabil e INormativaVigente
    // (LosValoresNacionalesSeLeenEnUnSoloSitio). Las revisiones, ReviewNationalValueCommand y ReviewRegulatoryDocumentCommand.
    DbSet<Domain.Entities.Core.NationalValues.NationalValue> NationalValues { get; }
    DbSet<Domain.Entities.Core.NationalValues.RegulatoryDocument> RegulatoryDocuments { get; }
    DbSet<Domain.Entities.Core.NationalValues.NationalValueReplication> NationalValueReplications { get; }
    DbSet<Domain.Entities.Core.NationalValues.NationalValueReview> NationalValueReviews { get; }

    // Feature 013 (T50; data-model parte 3 §4.5): el perfil regulatorio. Lo escribe sólo AddRegulatoryProfileVersionCommand y lo
    // lee IPerfilRegulatorio (con la regla bitemporal).
    DbSet<Domain.Entities.Core.RegulatoryProfileVersion> RegulatoryProfileVersions { get; }

    // Feature 013 (T0087, T0088; data-model parte 3 §4.8, §4.9): listas restrictivas (las escribe sólo ImportRestrictiveListCommand y
    // las lee la carga vigente de CARGA-PROPIA) y solicitudes del titular.
    DbSet<Domain.Entities.Compliance.RestrictiveListLoad> RestrictiveListLoads { get; }
    DbSet<Domain.Entities.Compliance.RestrictiveListEntry> RestrictiveListEntries { get; }
    DbSet<Domain.Entities.Compliance.DataSubjectRequest> DataSubjectRequests { get; }

    // Feature 013 (T0077, T16; data-model parte 3 §4.6): el registro de participantes de la integración. Lo escribe sólo
    // EscritorDelRegistroDeIntegracion y lo lee IRegistroDeIntegracion.
    DbSet<IntegrationParticipant> IntegrationParticipants { get; }
    DbSet<IntegrationParticipantAdoption> IntegrationParticipantAdoptions { get; }

    // Feature 013 (T0080, T72; data-model parte 3 §4.7): el canal externo por cooperativa y clase. Sólo guarda la referencia a la
    // credencial; lo escribe ConfigureExternalChannelCommand (y la verificación) y lo lee la credencial en archivo.
    DbSet<Domain.Entities.Core.ExternalChannelSetting> ExternalChannelSettings { get; }

    // Feature 013 (T0084, T76; data-model parte 3 §4.12): las plantillas de mensaje versionadas. Las escribe
    // CreateMessageTemplateVersionCommand (y la semilla); el mensaje al deudor sella PublicId y versión.
    DbSet<Domain.Entities.Core.NotificationTemplate> NotificationTemplates { get; }

    // Feature 013, fase 3 (T0236): Cartera nueva, migración CarteraNucleo. Sólo forma; cada historia les da comportamiento.
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CreditReportClaim> CreditReportClaims { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.DebtorMessage> DebtorMessages { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.DebtorMessageEvent> DebtorMessageEvents { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.PriorNotice> PriorNotices { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CommercialTermEquivalence> CommercialTermEquivalences { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.ContributionBalanceLoad> ContributionBalanceLoads { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLine> CreditLines { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineCollateralRule> CreditLineCollateralRules { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineDocumentRequirement> CreditLineDocumentRequirements { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineInsurance> CreditLineInsurances { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineRateBand> CreditLineRateBands { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.CreditLineVersion> CreditLineVersions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.LendingFactType> LendingFactTypes { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.LendingNumberSequence> LendingNumberSequences { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.LendingSetup> LendingSetups { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Configuration.UserCreditLineScope> UserCreditLineScopes { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedDocumentEvent> IssuedDocumentEvents { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedDocumentLoan> IssuedDocumentLoans { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedLendingDocument> IssuedLendingDocuments { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.RegulatoryFile> RegulatoryFiles { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.RegulatoryFileLine> RegulatoryFileLines { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.AttributionLevel> AttributionLevels { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.AttributionMatrix> AttributionMatrices { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.AttributionRule> AttributionRules { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.CaseParticipation> CaseParticipations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.ConnectedGroup> ConnectedGroups { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.ConnectedGroupMember> ConnectedGroupMembers { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.CreditDecision> CreditDecisions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.GoverningBody> GoverningBodies { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.GoverningBodyMember> GoverningBodyMembers { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.GoverningBodyRuleVersion> GoverningBodyRuleVersions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.RelatedParty> RelatedParties { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.Session> LendingSessions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.SessionAttendance> SessionAttendances { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.SessionItem> SessionItems { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Governance.SessionVote> SessionVotes { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Integration.AccountingFactPart> AccountingFactParts { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingInboundMessage> LendingInboundMessages { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingInboundMessageEvaluation> LendingInboundMessageEvaluations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingProcessRun> LendingProcessRuns { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Integration.LendingProcessRunError> LendingProcessRunErrors { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.InsolvencyProceeding> InsolvencyProceedings { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.InsuranceClaim> InsuranceClaims { get; }
    // Feature 013, US9 (T0697): riesgo y cierres (CarteraRiesgo).
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyClose> MonthlyCloses { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyCloseLoan> MonthlyCloseLoans { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyCloseGeneralImpairment> MonthlyCloseGeneralImpairments { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyCloseReopening> MonthlyCloseReopenings { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.LoanArrearsHistoryEntry> LoanArrearsHistory { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.AnalysisRating> AnalysisRatings { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.RiskPolicyVersion> RiskPolicyVersions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.ImpairmentModelElection> ImpairmentModelElections { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.ArrearsHistoryLoad> ArrearsHistoryLoads { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.Loan> Loans { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.LoanControl> LoanControls { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.LoanParty> LoanParties { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Loans.LoanTermsVersion> LoanTermsVersions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.LendingActivation> LendingActivations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationArrearsHistoryRow> MigrationArrearsHistoryRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationAuthorizationRow> MigrationAuthorizationRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationBalanceRow> MigrationBalanceRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationBlockRow> MigrationBlockRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationBureauReportRow> MigrationBureauReportRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationChargedOffRow> MigrationChargedOffRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationClaimRow> MigrationClaimRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationCoDebtorRow> MigrationCoDebtorRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationCollateralRow> MigrationCollateralRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationCreditLimitRow> MigrationCreditLimitRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationInstallmentRow> MigrationInstallmentRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoad> MigrationLoads { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoadDifference> MigrationLoadDifferences { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoadPart> MigrationLoadParts { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationLoanRow> MigrationLoanRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationPriorNoticeRow> MigrationPriorNoticeRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationRelatedPartyRow> MigrationRelatedPartyRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationRestructuringRow> MigrationRestructuringRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationRiskDataRow> MigrationRiskDataRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationSaleReconciliation> MigrationSaleReconciliations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.MigrationSaleRow> MigrationSaleRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Migration.ProvisionalCollection> ProvisionalCollections { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementFile> DisbursementFiles { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementFileLine> DisbursementFileLines { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementOrder> DisbursementOrders { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.DisbursementOrderResult> DisbursementOrderResults { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.InstallmentNonDeduction> InstallmentNonDeductions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanOperation> LoanOperations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.BureauQuery> BureauQueries { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.Collateral> Collaterals { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplication> CreditApplications { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplicationCheck> CreditApplicationChecks { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplicationDocument> CreditApplicationDocuments { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditApplicationParty> CreditApplicationParties { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditLimit> CreditLimits { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.CreditScore> CreditScores { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.DebtorBlock> DebtorBlocks { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.DebtorControl> DebtorControls { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.LoanNumberReservation> LoanNumberReservations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.PrepaymentPreference> PrepaymentPreferences { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.PromissoryNote> PromissoryNotes { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Origination.ScreeningResult> ScreeningResults { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.LoanPayrollLink> LoanPayrollLinks { get; }
    // K2 (CarteraLibranza): convenios de libranza, archivo de descuentos, resultados y seguimientos de los no aplicados.
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreement> PayrollAgreements { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementVersion> PayrollAgreementVersions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementContact> PayrollAgreementContacts { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementResultCode> PayrollAgreementResultCodes { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementFileGroup> PayrollAgreementFileGroups { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollAgreementConceptEquivalence> PayrollAgreementConceptEquivalences { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionFile> PayrollDeductionFiles { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionFileLine> PayrollDeductionFileLines { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionResult> PayrollDeductionResults { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionFileAccountLine> PayrollDeductionFileAccountLines { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionAccountResult> PayrollDeductionAccountResults { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.PayrollDeductionUnmatchedLine> PayrollDeductionUnmatchedLines { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Payroll.NonDeductionFollowUp> NonDeductionFollowUps { get; }
    // K3 (feature 013, CarteraNovedades): novedades, condonaciones, castigo y evaluación de cartera.
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanNovelty> LoanNovelties { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanNoveltyLoan> LoanNoveltyLoans { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.LoanNoveltyConsent> LoanNoveltyConsents { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.Waiver> Waivers { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.WaiverLine> WaiverLines { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.WriteOffBatch> WriteOffBatches { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Operations.WriteOffBatchItem> WriteOffBatchItems { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.PortfolioEvaluation> PortfolioEvaluations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Risk.PortfolioEvaluationLoan> PortfolioEvaluationLoans { get; }
    // K4 (feature 013, CarteraCobranza; data-model §26.3–§26.10).
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.MessageCampaign> MessageCampaigns { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectionStage> CollectionStages { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectionStageHistoryEntry> CollectionStageHistory { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectorAssignmentRule> CollectorAssignmentRules { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectorAssignment> CollectorAssignments { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.CollectionAction> CollectionActions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.PaymentPromise> PaymentPromises { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.PaymentAgreement> PaymentAgreements { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.PaymentAgreementInstallment> PaymentAgreementInstallments { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.LegalProcess> LegalProcesses { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Collections.LegalProcessAction> LegalProcessActions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Projections.LoanDailyPosition> LoanDailyPositions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Schedules.RepaymentInstallment> RepaymentInstallments { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Schedules.RepaymentScheduleVersion> RepaymentScheduleVersions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Transactions.LoanTransaction> LoanTransactions { get; }
    // Feature 014 (T021–T026): las cuentas de captación y aportes y su libro.
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.MemberAccount> MemberAccounts { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.MemberAccountParty> MemberAccountParties { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.GmfExemptAccount> GmfExemptAccounts { get; }
    // Feature 014, US11 (T187): bloqueos, embargos y respuestas al juzgado.
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions.AccountHold> AccountHolds { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions.Garnishment> Garnishments { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions.GarnishmentResponse> GarnishmentResponses { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountInstallment> AccountInstallments { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountInstallmentNonDeduction> AccountInstallmentNonDeductions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.ContributionRequest> ContributionRequests { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.MembershipWithdrawal> MembershipWithdrawals { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.MembershipWithdrawalLine> MembershipWithdrawalLines { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.AssemblyDistribution> AssemblyDistributions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.AssemblyDistributionLine> AssemblyDistributionLines { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.AssemblyReturnBase> AssemblyReturnBases { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.DeceasedHolderCase> DeceasedHolderCases { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes.UnclaimedBalance> UnclaimedBalances { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Liquidity.LiquidityFundSnapshot> LiquidityFundSnapshots { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Liquidity.LiquidityFundHolding> LiquidityFundHoldings { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Core.AssociateInboundMessage> AssociateInboundMessages { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Documents.IssuedDocumentAccount> IssuedDocumentAccounts { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountPayoutOrder> AccountPayoutOrders { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.MemberAccountOperation> MemberAccountOperations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.MemberAccountTransaction> MemberAccountTransactions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections.MemberAccountDailyPosition> MemberAccountDailyPositions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections.MemberAccountControl> MemberAccountControls { get; }
    // Feature 014, US1 (T060–T062): las versiones de las líneas de captación y de aportes.
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.DepositLineVersion> DepositLineVersions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.DepositLineRateTier> DepositLineRateTiers { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.ContributionLineVersion> ContributionLineVersions { get; }
    // Feature 014, US4 (T110, T114, T118): el GMF del mes, los cargos, sus exoneraciones y el débito automático.
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.GmfMonthlyUsage> GmfMonthlyUsages { get; }
    // Feature 014, US5 (T130): las liquidaciones de intereses y la retención acumulada por titular y año.
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.InterestSettlement> InterestSettlements { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.WithholdingAccumulation> WithholdingAccumulations { get; }
    // Feature 014, US9 (T151): filas de preparación de la migración de aportes y captaciones.
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration.MigrationLineEquivalenceRow> MigrationLineEquivalenceRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration.MigrationContributionAccountRow> MigrationContributionAccountRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration.MigrationAccountRow> MigrationAccountRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration.MigrationAccountPartyRow> MigrationAccountPartyRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration.MigrationAccountInstallmentRow> MigrationAccountInstallmentRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration.MigrationGmfExemptionRow> MigrationGmfExemptionRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration.MigrationHoldRow> MigrationHoldRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration.MigrationTaxYearRow> MigrationTaxYearRows { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.DepositLineCharge> DepositLineCharges { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountCharge> AccountCharges { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.AccountChargeExemption> AccountChargeExemptions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.DirectDebitMandate> DirectDebitMandates { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.MemberAccountTermsVersion> MemberAccountTermsVersions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.TermDepositScheduleItem> TermDepositScheduleItems { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration.NegotiatedRateRule> NegotiatedRateRules { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.NegotiatedRateRequest> NegotiatedRateRequests { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration.MigrationTermDepositRow> MigrationTermDepositRows { get; }

    // Admin
    // Tenants y TenantBranches se retiraron: son del plano de control del SaaS y
    // viven en IAdminDbContext. Quien las necesite desde un handler tiene que pedir
    // ese contexto y decirlo, en vez de alcanzarlas por la puerta de atras.

    // ---- Feature 015 (T0028, T0033, T0036, T0039; data-model §1.1, §3.1, §9.1, §16): plataforma y esqueleto de Recaudo y Tesorería ----
    DbSet<IngenIA365ERP.Domain.Entities.Core.PersonBankAccount> PersonBankAccounts { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Core.GoLive.GoLiveChecklist> GoLiveChecklists { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Core.GoLive.GoLiveChecklistItem> GoLiveChecklistItems { get; }
    /// <summary><c>COR_Sequences</c>: la numeración de Recaudo y Tesorería (clave en <c>Name</c>). Sólo la escriben sus numeradores.</summary>
    DbSet<IngenIA365ERP.Domain.Entities.Core.Sequence> Sequences { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.ReceiptsSetup> ReceiptsSetups { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Operations.PayerControl> PayerControls { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Treasury.Activation.TreasurySetup> TreasurySetups { get; }
    // ---- Feature 015, fases 3 a 6: Recaudo y Tesorería ----
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.UserCollectionPointScope> UserCollectionPointScopes { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.UserCashBoxScope> UserCashBoxScopes { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.UserPaymasterScope> UserPaymasterScopes { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Treasury.BankAccounts.UserBankAccountScope> UserBankAccountScopes { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.CollectionPoint> CollectionPoints { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.CashBox> CashBoxes { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.CashBoxCashier> CashBoxCashiers { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.PaymentMeansCollectionPoint> PaymentMeansCollectionPoints { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.ReceiptDocumentType> ReceiptDocumentTypes { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.MiscConcept> MiscConcepts { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.ContingencyBook> ContingencyBooks { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.SupervisorDelegation> SupervisorDelegations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Configuration.ChannelModalityRule> ChannelModalityRules { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Vaults.Vault> Vaults { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Vaults.VaultCustodian> VaultCustodians { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CollectionPointDay> CollectionPointDays { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CashSession> ReceiptsCashSessions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CashCount> ReceiptsCashCounts { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CashCountLine> ReceiptsCashCountLines { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CashCountDenomination> ReceiptsCashCountDenominations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CashCountTerminalBatch> ReceiptsCashCountTerminalBatches { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CashCountReference> CashCountReferences { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CashMovement> CashMovements { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CashMovementDenomination> CashMovementDenominations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CashSessionAdjustment> CashSessionAdjustments { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.DayClose> ReceiptsDayCloses { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.DayCloseLine> ReceiptsDayCloseLines { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.DailySquare> DailySquares { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CashierReceivable> CashierReceivables { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Sessions.CashSurplus> CashSurpluses { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Vaults.VaultMovement> VaultMovements { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Vaults.VaultMovementDenomination> VaultMovementDenominations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Vaults.VaultDenominationBalance> VaultDenominationBalances { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Operations.CashOperation> CashOperations { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Operations.Receipt> IssuedReceipts { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Operations.ReceiptMeans> ReceiptMeans { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Operations.ReceiptApplication> ReceiptApplications { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Operations.ReceiptApplicationPortion> ReceiptApplicationPortions { get; }
    DbSet<IngenIA365ERP.Domain.Entities.Receipts.Operations.SourceOfFundsDeclaration> SourceOfFundsDeclarations { get; }

    /// <summary>
    /// Descarta todo lo que el contexto tenga sin guardar (feature 009, R3): lo usa el reintento
    /// por concurrencia entre un intento y el siguiente. No toca la base.
    /// </summary>
    void DescartarCambios();

    /// <summary>
    /// Descarta todo lo que el contexto siga sin guardar <b>salvo</b> <paramref name="conservar"/>, que queda seguido como sin cambios con su
    /// token de concurrencia intacto (feature 013, US4, bloque 8C). Es lo que hace una tanda larga entre una parte y la siguiente: suelta el libro
    /// y las proyecciones y sigue con la corrida. No se puede hacer con <see cref="DescartarCambios"/> y volver a adjuntar: en PostgreSQL el token
    /// es la columna de sistema <c>xmin</c>, una propiedad sombra que se pierde al soltar la entidad, y el guardado siguiente fallaría por
    /// concurrencia (en SQL Server <c>RowVersion</c> viaja en la entidad y no se notaba). No toca la base.
    /// </summary>
    void DescartarCambiosSalvo(params object[] conservar);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
