namespace IngenIA365ERP.Application.Common.Audit;

/// <summary>
/// Constantes para los tipos de evento auditables introducidos por la feature
/// 002-identidad-central-federada (T032). El <c>AuditBehavior</c> existente
/// captura estos eventos automáticamente al pasar Commands por MediatR
/// (principio constitucional X — trazabilidad SIPLA/SARLAFT).
///
/// <para>
/// Convención: <c>{Aggregate}.{Action}.{Modifier?}</c>. Cada evento se persiste
/// en MongoDB <c>audit_events</c> con TTL 5 años heredado de Fase 0.
/// </para>
/// </summary>
public static class AuditEventTypes
{
    // -------------------- CentralUser --------------------
    public const string CentralUserLoginSuccess = "CentralUser.Login.Success";
    public const string CentralUserLoginFailed = "CentralUser.Login.Failed";
    public const string CentralUserLoginLocked = "CentralUser.Login.Locked";
    public const string CentralUserLogout = "CentralUser.Logout";

    public const string CentralUserMfaSuccess = "CentralUser.Mfa.Success";
    public const string CentralUserMfaFailed = "CentralUser.Mfa.Failed";
    public const string CentralUserMfaResetByMaster = "CentralUser.MfaResetByMaster";
    public const string CentralUserMfaRecoveryCodeUsed = "CentralUser.Mfa.RecoveryCodeUsed";
    public const string CentralUserMfaRecoveryCodeFailed = "CentralUser.Mfa.RecoveryCodeFailed";

    // -------------------- Invitations --------------------
    public const string InvitationIssuedByTenantAdmin = "Invitation.Issued.ByTenantAdmin";
    public const string InvitationIssuedByMaster = "Invitation.Issued.ByMaster";
    public const string InvitationAccepted = "Invitation.Accepted";
    public const string InvitationExpired = "Invitation.Expired";
    public const string InvitationRevoked = "Invitation.Revoked";
    public const string InvitationSuperseded = "Invitation.Superseded";

    // -------------------- Memberships --------------------
    public const string MembershipActivated = "Membership.Activated";
    public const string MembershipActivatedFromSuspension = "Membership.Activated.FromSuspension";
    public const string MembershipSuspended = "Membership.Suspended";
    public const string MembershipRevoked = "Membership.Revoked";
    public const string MembershipRevokedInvitationExpired = "Membership.Revoked.InvitationExpired";
    public const string MembershipPromotedToAdmin = "Membership.PromotedToAdmin";
    public const string MembershipDemotedFromAdmin = "Membership.DemotedFromAdmin";
    public const string MembershipLastAdminProtected = "Membership.LastAdminProtected";

    // -------------------- Sessions --------------------
    public const string SessionTenantSelected = "Session.TenantSelected";
    public const string SessionTenantSwitched = "Session.TenantSwitched";

    // -------------------- Tenant MFA policy --------------------
    public const string TenantMfaPolicyActivated = "TenantMfaPolicy.Activated";
    public const string TenantMfaPolicyDeactivated = "TenantMfaPolicy.Deactivated";

    /// <summary>
    /// Cambio en QUÉ métodos acepta la cooperativa, sin tocar si los exige. Evento
    /// propio porque no es ni activar ni desactivar: con el nombre de aquellos, un
    /// cambio que deja fuera a media cooperativa quedaría registrado como
    /// «activada» un día en que ya estaba activada.
    /// </summary>
    public const string TenantMfaPolicyMethodsChanged = "TenantMfaPolicy.MethodsChanged";

    /// <summary>Cambio en los métodos que acepta la plataforma para el maestro.</summary>
    public const string PlatformMfaPolicyMethodsChanged = "PlatformMfaPolicy.MethodsChanged";

    // ---------- Recuperación del segundo factor por correo ----------
    //
    // Los cuatro son eventos de seguridad de primera línea: describen un intento de
    // retirarle a alguien su segundo factor. Se separan —pedida, cancelada,
    // ejecutada, confirmación fallida— porque cada uno significa algo distinto el
    // día que haya que reconstruir un incidente, y «cancelada» seguida de nada es
    // exactamente la traza de un ataque que no prosperó.

    public const string MfaRecoveryRequested = "MfaRecovery.Requested";
    public const string MfaRecoveryCancelled = "MfaRecovery.Cancelled";
    public const string MfaRecoveryExecuted = "MfaRecovery.Executed";
    public const string MfaRecoveryConfirmFailed = "MfaRecovery.ConfirmFailed";

    // -------------------- Profile + Recovery --------------------
    public const string ProfileMfaEnrolled = "Profile.MfaEnrolled";
    public const string ProfileMfaDisabled = "Profile.MfaDisabled";

    /// <summary>Retiró UNA credencial, conservando las demás.</summary>
    public const string ProfileMfaCredentialRevoked = "Profile.MfaCredentialRevoked";

    /// <summary>Le cambió el nombre a una credencial. No altera la seguridad,
    /// pero sí quién puede reconocer qué dispositivo es cuál.</summary>
    public const string ProfileMfaCredentialRenamed = "Profile.MfaCredentialRenamed";
    public const string ProfileRecoveryCodesRegenerated = "Profile.RecoveryCodesRegenerated";
    public const string ProfilePasswordChanged = "Profile.PasswordChanged";
    public const string ProfilePasswordResetRequested = "Profile.PasswordResetRequested";
    public const string ProfilePasswordResetRequestedNoSuchEmail = "Profile.PasswordResetRequested.NoSuchEmail";
    public const string ProfilePasswordResetConsumed = "Profile.PasswordReset.Consumed";
    public const string ProfileDefaultTenantChanged = "Profile.DefaultTenantChanged";
    public const string ProfileDefaultTenantInvalidatedCleared = "Profile.DefaultTenantInvalidated.Cleared";

    // -------------------- Tenant (SaaS) --------------------
    public const string TenantCreated = "Tenant.Created";

    // -------------------- Nómina (feature 005) --------------------
    // Eventos explícitos además del AuditBehavior genérico (contracts/permissions.md §Auditoría):
    // llevan EntityPublicId y valores antes/después.
    public const string PayrollSalaryChanged = "Payroll.SalaryChanged";
    public const string PayrollRunApproved = "Payroll.Run.Approved";
    public const string PayrollRunReversed = "Payroll.Run.Reversed";
    public const string PayrollRunDiscarded = "Payroll.Run.Discarded";
    public const string PayrollPaymentsMarked = "Payroll.Payments.Marked";
    public const string PayrollPaymentMarkReverted = "Payroll.Payments.MarkReverted";
    public const string PayrollPayslipsSent = "Payroll.Payslips.Sent";
    public const string PayrollEmployeeWithholdingChanged = "Payroll.EmployeeWithholding.Changed";
    public const string PayrollRunExported = "Payroll.Run.Exported";

    // -------------------- Nómina completa (feature 010, R12) --------------------
    // Las liquidaciones especiales son corridas y comparten Approved/Reversed/Discarded de arriba
    // sólo en la forma; aquí llevan nombre propio para que la consulta distinga una prima de una
    // nómina. La exportación reutiliza PayrollRunExported (el contrato lo llama
    // «Payroll.Report.Exported»; el código que existe es «Payroll.Run.Exported» y no se duplica)
    // y el porcentaje P2 aprobado emite además PayrollEmployeeWithholdingChanged, como hoy.
    public const string PayrollSettlementCalculated = "Payroll.Settlement.Calculated";
    public const string PayrollSettlementApproved = "Payroll.Settlement.Approved";
    public const string PayrollSettlementReversed = "Payroll.Settlement.Reversed";
    public const string PayrollSettlementDiscarded = "Payroll.Settlement.Discarded";
    public const string PayrollSettlementDeductionAdjusted = "Payroll.Settlement.DeductionAdjusted";
    public const string PayrollEmployeeTerminated = "Payroll.Employee.Terminated";
    public const string PayrollEmployeeReinstated = "Payroll.Employee.Reinstated";
    public const string PayrollOpeningBalanceChanged = "Payroll.OpeningBalance.Changed";
    public const string PayrollVacationRegistered = "Payroll.Vacation.Registered";
    public const string PayrollVacationCancelled = "Payroll.Vacation.Cancelled";
    public const string PayrollWithholdingRateCalculated = "Payroll.WithholdingRate.Calculated";
    public const string PayrollWithholdingRateApproved = "Payroll.WithholdingRate.Approved";
    public const string PayrollPilaGenerated = "Payroll.Pila.Generated";
    public const string PayrollPilaUploaded = "Payroll.Pila.Uploaded";
    public const string PayrollElectronicPayrollGenerated = "Payroll.ElectronicPayroll.Generated";
    public const string PayrollElectronicPayrollTransmitted = "Payroll.ElectronicPayroll.Transmitted";
    public const string PayrollElectronicPayrollStatusChanged = "Payroll.ElectronicPayroll.StatusChanged";
    public const string PayrollElectronicPayrollEnablementChanged = "Payroll.ElectronicPayroll.EnablementChanged";
    public const string PayrollDispersionGenerated = "Payroll.Dispersion.Generated";
    public const string PayrollDispersionSent = "Payroll.Dispersion.Sent";
    public const string PayrollDispersionCancelled = "Payroll.Dispersion.Cancelled";
    public const string PayrollSeveranceDeposited = "Payroll.Severance.Deposited";
    public const string PayrollCompanyPolicyChanged = "Payroll.CompanyPolicy.Changed";
    public const string PayrollHolidayChanged = "Payroll.Holiday.Changed";

    // -------------------- Contabilidad (feature 009) --------------------
    // Eventos explicitos ademas del AuditBehavior generico: exportaciones (FR-050), envios de
    // certificados, inicializacion y validacion de catalogos. Modulo "Accounting".
    public const string AccountingSetupInitialized = "Accounting.Setup.Initialized";
    public const string AccountingCatalogValidated = "Accounting.Catalog.Validated";
    public const string AccountingReportExported = "Accounting.Report.Exported";
    public const string AccountingCertificateSent = "Accounting.Certificate.Sent";
    // Feature 012, I2 (contracts/contabilidad.md §10): el consumo de los mensajes de Inventario.
    public const string AccountingInventoryPosted = "Accounting.Inventory.Posted";
    public const string AccountingInventoryRejected = "Accounting.Inventory.Rejected";
    public const string AccountingInventoryBatchProcessed = "Accounting.Inventory.BatchProcessed";
    // Feature 012, T509 (api.md §26.1): la importación aplicada de la matriz de reglas de Inventario.
    public const string AccountingInventoryRulesImported = "Accounting.InventoryRules.Imported";
    // Feature 013, T0643 (api.md §23.1): la importación aplicada de la matriz de reglas de Cartera.
    public const string AccountingLendingRulesImported = "Accounting.LendingRules.Imported";
    // Feature 013, T0651 (contracts/contabilidad.md §22): el consumo de los hechos de Cartera y el registro de los indicadores regulatorios.
    public const string AccountingLendingPosted = "Accounting.Lending.Posted";
    public const string AccountingLendingRejected = "Accounting.Lending.Rejected";
    public const string AccountingLendingBatchProcessed = "Accounting.Lending.BatchProcessed";
    public const string AccountingRegulatoryIndicatorRegistered = "Accounting.RegulatoryIndicator.Registered";

    // -------------------- Inventario (feature 012, T44, T181) --------------------
    // Eventos explicitos que no salen de un comando: la exportacion de una vista de
    // /api/reports/inventory (vista, filtros, formato, filas) y la descarga de un catalogo con
    // datos (?withData=true: catalogo, filas). Los emite InventoryAuditEmitter, encadenados.
    public const string InventoryReportExported = "Inventory.Report.Exported";
    public const string InventoryCatalogExported = "Inventory.Catalog.Exported";
    // La verificacion de integridad del kardex (US2, T258): una consulta que deja su resultado (filtros, filas
    // revisadas, incidentes y la alerta). La reconstruccion la audita el AuditBehavior (es un comando).
    public const string InventoryIntegrityVerified = "Inventory.Integrity.Verified";
    // Las acciones de riesgo del POS (US5, T603, T606, T607; T50, F13): quitar una linea, descuento, precio manual,
    // suspender, recuperar, descartar, cobrar, entregar y reimprimir. Canal pos, dentro de la transaccion del comando
    // (AuditoriaDelPuntoDeVenta). Las lecturas del lector no emiten evento propio.
    public const string InventoryPosLineRemoved = "Inventory.Pos.LineRemoved";
    public const string InventoryPosDiscountApplied = "Inventory.Pos.DiscountApplied";
    public const string InventoryPosPriceOverridden = "Inventory.Pos.PriceOverridden";
    public const string InventoryPosSuspended = "Inventory.Pos.Suspended";
    public const string InventoryPosResumed = "Inventory.Pos.Resumed";
    public const string InventoryPosDiscarded = "Inventory.Pos.Discarded";
    public const string InventoryPosCheckout = "Inventory.Pos.Checkout";
    public const string InventoryDocumentDelivered = "Inventory.Document.Delivered";
    public const string InventoryDocumentReprinted = "Inventory.Document.Reprinted";

    // Feature 012, I3 (T617-T620): la caja. Abrir y cerrar la sesion, recontar tras un rechazo (con antes y despues), cerrar y
    // reabrir el dia (con motivo). Dentro de la transaccion del comando (AuditoriaDelPuntoDeVenta).
    public const string InventoryCashSessionOpened = "Inventory.CashSession.Opened";
    public const string InventoryCashSessionClosed = "Inventory.CashSession.Closed";
    public const string InventoryCashSessionRecounted = "Inventory.CashSession.Recounted";
    public const string InventoryDayCloseExecuted = "Inventory.DayClose.Executed";
    public const string InventoryDayCloseReopened = "Inventory.DayClose.Reopened";

    // Feature 012, I3 (T612): el reintegro de una nota por un medio que no fue de la venta (Inventory.Sales.RefundOtherMeans).
    public const string InventorySalesRefundOtherMeans = "Inventory.Sales.RefundOtherMeans";

    // Feature 012, I5 (T791): el envio de una orden de compra al proveedor (correo y numero). Lo emite InventoryAuditEmitter despues
    // del envio; el cierre del saldo de la orden (ClosePurchaseOrderBalanceCommand) lo audita el AuditBehavior con su motivo.
    public const string InventoryPurchaseOrderSent = "Inventory.PurchaseOrder.Sent";

    // -------------------- Navegacion (feature 009, FR-051) --------------------
    // La apertura de cada opcion del ERP la registra RegisterOptionAccessCommand por el
    // AuditBehavior (modulo "Navigation"); esta constante nombra el evento para las consultas.
    public const string NavigationOpened = "Navigation.Opened";

    // -------------------- Idempotencia (feature 012, T13) --------------------
    // Una operacion repetida con la misma Idempotency-Key devolvio el resultado guardado sin ejecutar
    // otra vez; lo emite IdempotencyBehavior con la clave y el primer uso en la metadata.
    public const string OperationReplayed = "Operation.Replayed";

    // -------------------- Auditoria de comandos (feature 012, T36) --------------------
    // Un comando que termino en Result.IsFailure: queda con este Action y el Error.Code en la metadata
    // (clave ErrorCode). Una excepcion sigue siendo «Failed».
    public const string CommandRejected = "Rejected";
    public const string CommandFailed = "Failed";

    // -------------------- Integridad de la auditoria (feature 012, T38) --------------------
    // POST /api/audit/integrity/verify: la verificacion misma, con su resultado en la metadata.
    public const string AuditLogIntegrityVerified = "AuditLog.IntegrityVerified";

    // -------------------- Autorizacion de datos al crear (feature 012, T46; T175) --------------------
    // Alta de una persona con AutorizacionAlCrear cuando la cooperativa no tiene politica publicada: el alta
    // procede y queda la constancia «sin politica vigente» (la alerta Personas.SinPoliticaDeDatos es de I3, T616).
    public const string PersonDataAuthorizationNoCurrentPolicy = "Person.DataAuthorization.NoCurrentPolicy";

    // -------------------- Cartera (feature 013, T67, C89; research §13 y R136) --------------------
    // Lecturas sensibles: la lista fija que emite LendingAuditEmitter (LasLecturasSensiblesDeCarteraSeAuditan). La consulta de
    // cupo de Comercial usa el EventId de Lending.CreditStatus.Queried como EvidenceId.
    public const string LendingPayoffViewed = "Lending.Payoff.Viewed";
    public const string LendingCreditBureauReportViewed = "Lending.CreditBureauReport.Viewed";
    public const string LendingDocumentReprinted = "Lending.Document.Reprinted";
    public const string LendingDocumentVerified = "Lending.Document.Verified";
    public const string LendingReportExported = "Lending.Report.Exported";
    public const string LendingSessionViewed = "Lending.Session.Viewed";
    public const string LendingRelatedPartyViewed = "Lending.RelatedParty.Viewed";
    public const string LendingCreditStatusQueried = "Lending.CreditStatus.Queried";
    public const string LendingAccessOutOfScope = "Lending.Access.OutOfScope";
    public const string LendingScreeningMatchReleased = "Lending.ScreeningMatch.Released";

    // US3 (T0444, T23): la distribución completa por cuota y concepto de una operación del libro (desembolso, reverso); no es lectura.
    public const string LendingOperationRegistered = "Lending.Operation.Registered";

    // US8 (T0752): la activación de Cartera con sus requisitos, conteos y huellas; no es lectura.
    public const string LendingActivationRegistered = "Lending.Activation.Registered";

    // K3, bloque D (decisión del dueño del 2026-10-15): el crédito que se excluye de un lote de castigo al aplicarlo porque ya no cumple; no es lectura.
    public const string LendingWriteOffItemExcluded = "Lending.WriteOffItem.Excluded";

    // K4 (T0944; C64): el evento resumen del registro de la etapa de cobranza migrada al desplegar K4, por cooperativa, con la tabla de equivalencia
    // de códigos; no es lectura.
    public const string LendingMigratedCollectionStagesRegistered = "Lending.CollectionStages.MigratedRegistered";
    /// <summary>K6a (T1034): la adopción o el desistimiento voluntario del modelo de pérdida esperada.</summary>
    public const string LendingImpairmentModelElectionRegistered = "Lending.ImpairmentModel.ElectionRegistered";
    /// <summary>K6a (T1035; C46): el resumen de una carga complementaria del historial de mora.</summary>
    public const string LendingArrearsHistoryLoadApplied = "Lending.ArrearsHistory.LoadApplied";

    // US13 (T0784, T0785): la emisión de un documento al deudor (también lectura de sus datos: cuenta como lectura sensible, nota 10 de PG) y su
    // anulación manual. La reimpresión y la verificación son Lending.Document.Reprinted y .Verified, de la lista cerrada.
    public const string LendingDocumentIssued = "Lending.Document.Issued";
    public const string LendingDocumentVoided = "Lending.Document.Voided";

    // US3 (T0454, bloque 7B): la emisión del enlace de descarga de un archivo de desembolsos (adaptadores §6.2: «enlace firmado de 60 s, auditado»).
    public const string LendingDisbursementFileDownloaded = "Lending.DisbursementFile.Downloaded";

    // K2 (T0836; C46): el evento resumen de cada archivo de descuentos (sus líneas van con [SinDiffDeAuditoria]) y el enlace de descarga.
    public const string LendingPayrollDeductionFileGenerated = "Lending.PayrollDeductionFile.Generated";
    public const string LendingPayrollDeductionFileDownloaded = "Lending.PayrollDeductionFile.Downloaded";

    // K5 (US16, T1004; C46): el evento resumen de cada generación regulatoria (SICSES o central; las líneas de las centrales van con
    // [SinDiffDeAuditoria]), su marca de cargado con el radicado y el enlace de descarga (trae deudores: se registra cada emisión).
    public const string LendingRegulatoryFileGenerated = "Lending.RegulatoryFile.Generated";
    public const string LendingRegulatoryFileUploaded = "Lending.RegulatoryFile.Uploaded";
    public const string LendingRegulatoryFileDownloaded = "Lending.RegulatoryFile.Downloaded";

    // US5 (bloque 9B, T0546; interfaces §3.13; T68, A11): el contrato de obligaciones. El intento va por un contexto aparte (sobrevive a la
    // reversión del recaudador); el hecho, en COR_AuditOutbox dentro de la transacción con la distribución completa. Las lecturas del contrato
    // (listar y simular) llevan D, R y la huella; no son de la lista cerrada de lecturas sensibles.
    public const string LendingObligationContractAttempted = "Lending.ObligationContract.Attempted";
    public const string LendingObligationContractApplied = "Lending.ObligationContract.Applied";
    public const string LendingObligationContractReversed = "Lending.ObligationContract.Reversed";
    public const string LendingObligationContractCreditBalanceResolved = "Lending.ObligationContract.CreditBalanceResolved";
    public const string LendingObligationContractRejected = "Lending.ObligationContract.Rejected";
    public const string LendingObligationContractListed = "Lending.ObligationContract.Listed";
    public const string LendingObligationContractSimulated = "Lending.ObligationContract.Simulated";

    // Feature 014, US3 (T083; contrato-de-cuentas §3.15): el contrato de cuentas, con la misma mecánica que el de obligaciones —el intento por un
    // contexto aparte, el hecho en COR_AuditOutbox dentro de la transacción con el antes y el después por concepto—. Consultar, listar y simular son
    // lecturas sensibles de saldos del titular (FR-003).
    public const string LendingAccountContractAttempted = "Lending.AccountContract.Attempted";
    public const string LendingAccountContractDeposited = "Lending.AccountContract.Deposited";
    public const string LendingAccountContractWithdrawn = "Lending.AccountContract.Withdrawn";
    public const string LendingAccountContractTransferred = "Lending.AccountContract.Transferred";
    public const string LendingAccountContractReversed = "Lending.AccountContract.Reversed";
    public const string LendingAccountContractRejected = "Lending.AccountContract.Rejected";
    public const string LendingAccountContractQueried = "Lending.AccountContract.Queried";
    public const string LendingAccountContractListed = "Lending.AccountContract.Listed";
    public const string LendingAccountContractSimulated = "Lending.AccountContract.Simulated";

    /// <summary>Feature 014, US4 (T123; api.md §4): el libro o las operaciones de la cuenta de un titular, vistos en la pantalla.</summary>
    public const string LendingSavingsTransactionsViewed = "Lending.Savings.TransactionsViewed";

    // Feature 014, US3 (T086): la devolución de aportes sin retiro y la cesión entre asociados, pedidas y decididas por personas distintas.
    public const string LendingContributionRefundRequested = "Lending.ContributionRefund.Requested";
    public const string LendingContributionRefundApproved = "Lending.ContributionRefund.Approved";
    public const string LendingContributionAssignmentRequested = "Lending.ContributionAssignment.Requested";
    public const string LendingContributionAssignmentApproved = "Lending.ContributionAssignment.Approved";

    // US6 (la reutiliza): la reserva y liberación de números de Comercial.
    public const string LendingNumberReservationAttempted = "Lending.NumberReservation.Attempted";
    public const string LendingNumberReservationReserved = "Lending.NumberReservation.Reserved";
    public const string LendingNumberReservationReleased = "Lending.NumberReservation.Released";

    // Intentos del grupo /api/lending (T67, F9): el 404 por falta de permiso y el 400 del validador, que corre antes que
    // AuditBehavior. Los registra el metadato AuditarIntentoDenegado de la API por AuditoriaDeIntentos.
    public const string LendingAccessDenied = "Lending.Access.Denied";
    public const string LendingRequestRejected = "Lending.Request.Rejected";

    // -------------------- Database (feature 004 multi-motor) --------------------
    public const string DatabaseSeedExecuted = "Database.Seed.Executed";
    public const string DatabaseSeedTestSeedEnabledInProduction = "Database.Seed.TestSeedEnabledInProduction";
}
