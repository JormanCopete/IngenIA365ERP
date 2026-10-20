using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Feature 013, T0132 (C65; data-model §9.1, parte 1 §10, parte 2 «Nombres fijados»; DT §7 C112, C123): cada enumeración de
/// <c>Domain/Enums/Lending</c> tiene exactamente los nombres y números fijados. Se guardan por su número y un valor nunca se renumera:
/// esta prueba es la que lo nota. También fija que todo enum del espacio está en la lista (uno nuevo se agrega aquí a propósito).
/// </summary>
public class EnumeracionesDeCarteraTests
{
    private static readonly Dictionary<Type, (string Nombre, int Numero)[]> Esperados = new()
    {
        // ------------------------------------------------------------------------------------- crédito, libro, operaciones --
        [typeof(LoanConcept)] = [("Principal", 1), ("CurrentInterest", 2), ("DefaultInterest", 3), ("CurrentInterestInSuspense", 4),
            ("DefaultInterestInSuspense", 5), ("Insurance", 6), ("CollectionFee", 7), ("OtherCharge", 8), ("Advance", 9), ("CreditBalance", 10)],
        [typeof(LoanTransactionKind)] = [("Disbursement", 1), ("OpeningBalance", 2), ("Accrual", 3), ("AccrualAdjustment", 4),
            ("SuspenseReclassification", 5), ("PaymentAllocation", 6), ("PaymentReallocation", 7), ("AdvanceReceived", 8),
            ("AdvanceApplied", 9), ("CreditBalanceRecognized", 10), ("CreditBalanceSettled", 11), ("Charge", 12), ("Waiver", 13),
            ("CommercialAdjustment", 14), ("WriteOff", 15), ("Recovery", 16),
            // K3 (T0874): la capitalización y el traslado al crédito enlazado de una novación o refinanciación.
            ("Capitalization", 17), ("TransferToLinkedLoan", 18), ("TransferFromLinkedLoan", 19),
            // K6b (T1054, T1056): la corrección monetaria de un crédito en UVR.
            ("MonetaryCorrection", 20)],
        [typeof(LoanOperationKind)] = [("Disbursement", 1), ("OpeningBalance", 2), ("Payment", 3), ("PaymentReversal", 4),
            ("DisbursementReversal", 5), ("RateReset", 6), ("TermsChange", 7), ("Charge", 8), ("Waiver", 9), ("CommercialAdjustment", 10),
            ("WriteOff", 11), ("PreferenceChange", 12), ("CreditBalanceResolution", 13), ("ProvisionalCollection", 14)],
        [typeof(LoanStatus)] = [("PendingDisbursement", 1), ("Active", 2), ("PaidOff", 3), ("WrittenOff", 4), ("Voided", 5), ("Novated", 6)],
        [typeof(LoanOrigin)] = [("Origination", 1), ("Commercial", 2), ("Migration", 3)],
        [typeof(SaleObligationStatus)] = [("Validated", 1), ("ValidationFailed", 2), ("Migrated", 3), ("CancelledBeforeCutoff", 4)],
        [typeof(LoanLinkKind)] = [("Replacement", 1), ("Novation", 2), ("Refinancing", 3)],
        [typeof(TermsVersionCause)] = [("Origination", 1), ("CommercialSale", 2), ("Migration", 3), ("RateReset", 4), ("Restructuring", 5),
            ("Modification", 6), ("Extension", 7), ("PaymentDateChange", 8), ("RateChange", 9), ("Novation", 10)],
        [typeof(DisbursementForm)] = [("Transfer", 1), ("Check", 2), ("ThirdPartyPayment", 3), ("InKind", 4)],
        [typeof(DisbursementOrderStatus)] = [("Issued", 1), ("Disbursed", 2), ("Rejected", 3), ("CheckNotCashed", 4), ("Voided", 5)],
        [typeof(LoanNumberReservationStatus)] = [("Reserved", 1), ("Used", 2), ("Released", 3), ("Expired", 4)],
        [typeof(LendingNumberKind)] = [("Loan", 1), ("Operation", 2), ("Application", 3), ("DisbursementOrder", 4), ("DisbursementFile", 5),
            ("SessionMinutes", 6), ("IssuedDocument", 7), ("PayrollDeductionFile", 8), ("RegulatoryFile", 9), ("LoanNovelty", 10), ("Waiver", 11),
            ("WriteOffBatch", 12), ("PortfolioEvaluation", 13), ("PaymentAgreement", 14), ("LegalProcess", 15),
            // Feature 014 (T014; research E03).
            ("MemberAccount", 16), ("MemberAccountOperation", 17), ("MembershipWithdrawal", 18), ("AssemblyDistribution", 19), ("DeceasedHolderCase", 20)],

        // ---------------------------------------------------------------------------------------- condiciones financieras --
        [typeof(DayCountConvention)] = [("Thirty360", 1), ("Actual365", 2), ("Actual360", 3)],
        [typeof(RateEquivalence)] = [("Compound", 1), ("Linear", 2)],
        [typeof(AmortizationSystem)] = [("LevelPayment", 1), ("ConstantPrincipal", 2), ("Bullet", 3)],
        [typeof(GraceKind)] = [("None", 0), ("Principal", 1), ("Total", 2)],
        [typeof(GraceInterestTreatment)] = [("FirstInstallment", 1), ("Distributed", 2)],
        [typeof(PaymentModality)] = [("FullPayoff", 1), ("PastDue", 2), ("CycleInstallment", 3), ("Advance", 4), ("ExtraordinaryPrincipal", 5),
            ("SpecificInstallment", 6), ("FreeAmount", 7)],
        [typeof(PrepaymentChoice)] = [("ReduceTerm", 1), ("ReduceInstallment", 2)],
        [typeof(AllocationOrder)] = [("OldestInstallmentFirst", 1), ("ConceptFirst", 2)],
        [typeof(RoundingMoment)] = [("PerConcept", 1), ("PerInstallment", 2), ("PerTotal", 3)],
        [typeof(RoundingMode)] = [("AwayFromZero", 1), ("ToEven", 2), ("Up", 3), ("Down", 4)],
        [typeof(RateKind)] = [("Fixed", 1), ("Variable", 2)],
        // K6b (T1048): la unidad del capital de la línea y del crédito.
        [typeof(DenominationUnit)] = [("Pesos", 1), ("Uvr", 2)],
        [typeof(DefaultRateKind)] = [("Fixed", 1), ("RelativeToCurrent", 2)],
        // El valor ES la base de proporción (precedente de PayrollPeriodicity; CH39).
        [typeof(RepaymentPeriodicity)] = [("SinglePayment", 0), ("Weekly", 7), ("TenDay", 10), ("Biweekly", 15), ("Monthly", 30),
            ("Bimonthly", 60), ("Quarterly", 90), ("SemiAnnual", 180), ("Annual", 360)],
        [typeof(RepaymentChannel)] = [("Cashier", 1), ("OwnPayroll", 2), ("PaymasterDeduction", 3), ("DirectDebit", 4)],
        [typeof(InsuranceBase)] = [("OutstandingBalance", 1), ("InitialAmount", 2)],
        [typeof(InsuranceAccrualTiming)] = [("AtDueDate", 1), ("Daily", 2)],
        [typeof(InterestTaxTreatment)] = [("NotTaxed", 1), ("Taxed", 2)],

        // ---------------------------------------------------------------------------------- configuración y originación --
        [typeof(CreditLineStatus)] = [("Draft", 1), ("Active", 2), ("Closed", 3)],
        [typeof(CreditLineClosedReason)] = [("Manual", 1), ("ModalityRetired", 2)],
        [typeof(DebtorKind)] = [("Associate", 1), ("Customer", 2)],
        [typeof(CommercialTermKind)] = [("Periodicity", 1), ("TermUnit", 2)],
        [typeof(ApplicationKind)] = [("Loan", 1), ("RevolvingLimit", 2)],
        [typeof(CreditApplicationStatus)] = [("Draft", 1), ("Submitted", 2), ("PendingDecision", 3), ("Returned", 4), ("Approved", 5),
            ("Denied", 6), ("Disbursed", 7), ("Granted", 8), ("Cancelled", 9)],
        [typeof(CreditPartyRole)] = [("Applicant", 1), ("CoDebtor", 2), ("Guarantor", 3)],
        [typeof(ApplicationCheckKind)] = [("RestrictiveLists", 1), ("CreditBureau", 2), ("PaymentCapacity", 3), ("DeductionCapacity", 4),
            ("ConsolidatedExposure", 5), ("CreditLimit", 6), ("Concentration", 7), ("Usury", 8), ("LineRequirements", 9),
            ("DataAuthorization", 10), ("Scoring", 11), ("RelatedParty", 12), ("Documents", 13)],
        [typeof(CheckStage)] = [("AtSubmission", 1), ("BeforeDecision", 2), ("BeforeDisbursement", 3)],
        [typeof(CheckOutcome)] = [("Passed", 1), ("Warning", 2), ("Blocking", 3), ("NotApplicable", 4), ("Unavailable", 5)],
        // Feature 014, US2 (T073): la apertura de una cuenta de captación o de aportes, al final.
        [typeof(ScreeningContext)] = [("Application", 1), ("Sale", 2), ("CreditLimit", 3), ("ListRescan", 4), ("AccountOpening", 5)],
        [typeof(ScreeningOutcome)] = [("NoMatch", 1), ("PossibleMatch", 2), ("Match", 3), ("Unavailable", 4)],
        [typeof(BureauQueryPurpose)] = [("Application", 1), ("CreditLimit", 2)],
        [typeof(CollateralKind)] = [("PromissoryNote", 1), ("CoDebtor", 2), ("Pledge", 3), ("Mortgage", 4), ("GuaranteeFund", 5), ("Contributions", 6)],
        [typeof(CollateralStatus)] = [("Constituted", 1), ("Valid", 2), ("Released", 3), ("Extinguished", 4), ("Executed", 5)],
        [typeof(PromissoryNoteKind)] = [("Physical", 1), ("Dematerialized", 2)],
        [typeof(PromissoryNoteStatus)] = [("Created", 1), ("PendingSignature", 2), ("Signed", 3), ("InCustody", 4), ("Endorsed", 5), ("Voided", 6)],
        [typeof(CreditLimitKind)] = [("Consolidated", 1), ("ByLine", 2), ("Revolving", 3)],
        [typeof(CreditLimitOrigin)] = [("Calculated", 1), ("Assigned", 2), ("Migration", 3)],
        [typeof(DebtorBlockReason)] = [("Manual", 1), ("Arrears", 2), ("ValidationFailed", 3), ("WrittenOff", 4), ("RestrictiveListMatch", 5),
            ("AssociateWithdrawal", 6), ("Deceased", 7), ("Insolvency", 8), ("Garnishment", 9), ("LimitRevalidation", 10)],
        [typeof(PreferenceOrigin)] = [("Application", 1), ("CreditLimitGrant", 2), ("DebtorRequest", 3), ("Migration", 4), ("InheritedFromLimit", 5)],

        // ------------------------------------------------------------------------------------------------------ gobierno --
        [typeof(CreditDecisionOutcome)] = [("Approve", 1), ("ApproveWithConditions", 2), ("Deny", 3), ("Return", 4)],
        [typeof(RelatedPartyBasis)] = [("BoardMember", 1), ("OversightBoardMember", 2), ("LegalRepresentative", 3), ("SignificantContributor", 4),
            ("Spouse", 5), ("Relative", 6)],
        [typeof(AttributionLevelKind)] = [("Individual", 1), ("Collegiate", 2)],
        [typeof(GoverningBodyKind)] = [("BoardOfDirectors", 1), ("OversightBoard", 2), ("CreditCommittee", 3), ("RiskCommittee", 4),
            ("Management", 5), ("Other", 99)],
        [typeof(FractionComparison)] = [("AtLeast", 1), ("MoreThan", 2)],
        [typeof(MajorityBase)] = [("PresentEligible", 1), ("Composition", 2)],
        [typeof(BodyMemberRole)] = [("Chair", 1), ("Secretary", 2), ("Member", 3), ("Alternate", 4)],
        [typeof(SessionKind)] = [("Ordinary", 1), ("Extraordinary", 2)],
        [typeof(SessionMode)] = [("InPerson", 1), ("Virtual", 2), ("Mixed", 3)],
        [typeof(SessionStatus)] = [("Draft", 1), ("Open", 2), ("Closed", 3)],
        [typeof(AttendanceStatus)] = [("Present", 1), ("Absent", 2), ("Excused", 3)],
        [typeof(SessionItemKind)] = [("Decision", 1), ("Recommendation", 2)],
        [typeof(VotingRule)] = [("Majority", 1), ("RelatedPartyQualified", 2)],
        [typeof(VoteValue)] = [("For", 1), ("Against", 2), ("Abstain", 3)],
        [typeof(VoteMethod)] = [("RecordedBySecretary", 1), ("OwnSession", 2), ("InPersonPasskey", 3), ("InPersonTotp", 4)],
        [typeof(KinshipKind)] = [("Consanguinity", 1), ("Affinity", 2), ("Civil", 3)],
        [typeof(RelatedPartySource)] = [("Manual", 1), ("Import", 2), ("Migration", 3)],
        [typeof(ConnectedGroupBasis)] = [("EconomicLink", 1), ("LegalLink", 2), ("CommonCoDebtor", 3), ("RelatedParty", 4)],

        // ------------------------------------------------------------------------------- integración, procesos, migración --
        [typeof(InboundMessageStatus)] = [("Processed", 1), ("ValidationFailed", 2), ("Rejected", 3), ("NotIncorporated", 4), ("ConfirmedByException", 5)],
        [typeof(InboundEvaluationOutcome)] = [("Accepted", 1), ("ValidationFailed", 2), ("NotConvertible", 3), ("NotIncorporated", 4)],
        // Las cuatro clases de corrida de C59 y el vocabulario de C123.
        [typeof(LendingProcessKind)] = [("DailyCycle", 1), ("MonthlyClose", 2), ("CloseReopen", 3), ("NationalValueCorrection", 4),
            ("ScheduledTask", 5), ("MigratedCollectionStages", 6)],
        [typeof(LendingProcessRunStatus)] = [("Requested", 0), ("Running", 1), ("Completed", 2), ("CompletedWithErrors", 3), ("Failed", 4)],
        [typeof(AccountingFactPartSource)] = [("DailyCycleBatch", 1), ("LoanCatchUp", 2), ("NationalValueCorrectionBatch", 3), ("MonthlyClose", 4),
            ("CloseReopening", 5), ("AccountCatchUp", 6)],
        [typeof(MigrationLoadStatus)] = [("Draft", 1), ("Applied", 2), ("Replaced", 3), ("Activated", 4)],
        [typeof(MigrationPartStatus)] = [("Imported", 1), ("Superseded", 2)],
        [typeof(MigrationDifferenceKind)] = [("BalanceCheck", 1), ("SaleNotMigrated", 2), ("SaleOutsideLending", 3)],
        [typeof(SaleReconciliationOutcome)] = [("Migrated", 1), ("CancelledBeforeCutoff", 2), ("NotMigratedWithBalance", 3), ("OutsideLendingD29", 4)],

        // ------------------------------------------------------------------------------------------------ riesgo y cierres --
        [typeof(RatingCategory)] = [("A", 1), ("B", 2), ("C", 3), ("D", 4), ("E", 5)],
        [typeof(RatingSubcategory)] = [("None", 0), ("E1", 1), ("E2", 2)],
        [typeof(RatingCause)] = [("Arrears", 1), ("Model", 2), ("Drag", 3), ("Restructured", 4), ("Evaluation", 5), ("Analysis", 6),
            ("SingleInstallment", 7), ("NewLoan", 8), ("Insolvency", 9), ("Homologation", 10)],
        [typeof(ImpairmentModel)] = [("ArrearsAnnex1", 1), ("ExpectedLossAnnex2", 2)],
        [typeof(ModelBasis)] = [("NotObligated", 1), ("Obligated", 2), ("Voluntary", 3)],
        [typeof(MonthlyCloseKind)] = [("Base", 1), ("Monthly", 2)],
        [typeof(MonthlyCloseStatus)] = [("Closed", 1), ("Reopened", 2)],
        [typeof(ArrearsHistoryOrigin)] = [("Migration", 1), ("ComplementaryLoad", 2), ("MonthlyClose", 3)],
        [typeof(ImpairmentModelElectionKind)] = [("VoluntaryAdoption", 1), ("VoluntaryWithdrawal", 2)],
        [typeof(PortfolioEvaluationStatus)] = [("Draft", 1), ("Recommended", 2), ("Approved", 3), ("Applied", 4), ("Rejected", 5), ("Discarded", 6)],
        [typeof(RiskPolicyVersionStatus)] = [("PendingValidation", 1), ("Validated", 2)],

        // ---------------------------------------------------------------------------------- libranza, novedades y seguros --
        [typeof(NonDeductionOrigin)] = [("Contract", 1), ("Activation", 2), ("PaymasterResult", 3)],
        [typeof(NonDeductionReason)] = [("DeductionCap", 1), ("InsufficientNet", 2), ("Termination", 3), ("Leave", 4), ("Disability", 5),
            ("DeductionPriority", 6), ("Other", 99)],
        [typeof(LoanPayrollLinkOrigin)] = [("Origination", 1), ("Migration", 2), ("Transfer", 3), ("CommercialSale", 4), ("ChannelChange", 5)],
        [typeof(PayrollAgreementStatus)] = [("Active", 1), ("Suspended", 2), ("Closed", 3)],
        [typeof(PayrollResultOutcome)] = [("Deducted", 1), ("PartiallyDeducted", 2), ("NotDeducted", 3)],
        [typeof(NonDeductionFollowUpAction)] = [("DebtorNotified", 1), ("IncludedInNextFile", 2), ("MovedToCashPayment", 3), ("SentToCollection", 4), ("Closed", 5)],
        [typeof(PayrollFileStatus)] = [("Generated", 1), ("Superseded", 2), ("Sent", 3), ("Voided", 4)],
        [typeof(PayrollNoveltyKind)] = [("New", 1), ("Change", 2), ("Cancellation", 3), ("Unchanged", 4)],
        [typeof(NoveltyMechanism)] = [("Extension", 1), ("PaymentDateChange", 2), ("RateChange", 3), ("Novation", 4), ("Refinancing", 5),
            ("BranchChange", 6)],
        [typeof(NoveltyCause)] = [("Restructuring", 1), ("Modification", 2)],
        [typeof(LoanNoveltyStatus)] = [("Draft", 1), ("Submitted", 2), ("Approved", 3), ("Rejected", 4), ("Applied", 5), ("Cancelled", 6)],
        [typeof(NoveltyLoanRole)] = [("Modified", 1), ("Collected", 2), ("Resulting", 3)],
        [typeof(ConsentPartyRole)] = [("CoDebtor", 1), ("Guarantor", 2), ("CollateralOwner", 3)],
        [typeof(ConsentStatus)] = [("Pending", 1), ("Accepted", 2), ("Refused", 3)],
        [typeof(ConsentRefusalResolution)] = [("DoNotApply", 1), ("ApplyWithGuaranteeExtinguished", 2)],
        [typeof(WaiverStatus)] = [("Draft", 1), ("Submitted", 2), ("Approved", 3), ("Rejected", 4), ("Applied", 5), ("Cancelled", 6)],
        [typeof(WriteOffBatchStatus)] = [("Draft", 1), ("Submitted", 2), ("Approved", 3), ("Rejected", 4), ("Applied", 5), ("Cancelled", 6)],
        [typeof(WriteOffItemStatus)] = [("Included", 1), ("Rejected", 2), ("WrittenOff", 3)],
        [typeof(InsuranceClaimStatus)] = [("InProcess", 1), ("Paid", 2), ("Denied", 3), ("Withdrawn", 4)],
        [typeof(InsolvencyProceedingStatus)] = [("Negotiating", 1), ("Agreement", 2), ("Failed", 3)],
        [typeof(InsolvencyRegime)] = [("BusinessReorganization", 1), ("NaturalPersonDebtNegotiation", 2)],

        // ---------------------------------------------------------------- mensajes, cobranza, documentos y regulatorios --
        [typeof(ContactRole)] = [("Debtor", 1), ("CoDebtor", 2), ("Paymaster", 3)],
        [typeof(DebtorMessageEventKind)] = [("Queued", 1), ("Deferred", 2), ("Excluded", 3), ("Sent", 4), ("Delivered", 5), ("Failed", 6),
            ("Simulated", 7), ("Cancelled", 8)],
        [typeof(DebtorMessagePurpose)] = [("Collection", 1), ("PriorNotice", 2), ("Informative", 3), ("DocumentDelivery", 4), ("CollectionFeeNotice", 5)],
        [typeof(MessageChannel)] = [("Email", 1), ("Sms", 2), ("WhatsApp", 3), ("Letter", 4)],
        // T0304: fijados en adaptadores-y-archivos.md §4.2 y §4.5 (data-model §9 no los numera: van en el orden del contrato desde 1).
        [typeof(DebtorMessageOrigin)] = [("Operation", 1), ("Process", 2), ("PriorNotice", 3), ("Campaign", 4), ("Manual", 5), ("IssuedDocument", 6)],
        [typeof(ContactWindowReason)] = [("RecipientNotObligated", 1), ("DebtorDeceased", 2), ("Insolvency", 3), ("CollectionSuspended", 4),
            ("ChannelNotAuthorized", 5), ("AuthorizationWithoutEvidence", 6), ("NoDestination", 7), ("Sunday", 8), ("Holiday", 9),
            ("OutsideHours", 10), ("DailyLimitReached", 11), ("WeeklyChannelLocked", 12), ("NoWindow", 13), ("ChannelUnavailable", 14)],
        [typeof(CollectionActionChannel)] = [("Phone", 1), ("InPerson", 2), ("Email", 3), ("Sms", 4), ("WhatsApp", 5), ("Letter", 6), ("Visit", 7), ("System", 8)],
        [typeof(CollectionActionResult)] = [("NoAnswer", 1), ("WrongNumber", 2), ("ContactedNoCommitment", 3), ("PromiseToPay", 4), ("Refused", 5),
            ("RequestsAgreement", 6), ("ReportsDeceased", 7), ("Other", 8), ("LeftCollection", 9)],
        [typeof(AssignmentDistribution)] = [("ByCount", 1), ("ByBalance", 2)],
        [typeof(PaymentPromiseStatus)] = [("Pending", 1), ("Kept", 2), ("Broken", 3)],
        [typeof(PaymentAgreementStatus)] = [("Proposed", 1), ("Approved", 2), ("Rejected", 3), ("Cancelled", 4)],
        [typeof(PaymentAgreementPerformance)] = [("Current", 1), ("Fulfilled", 2), ("Broken", 3)],
        [typeof(LegalProcessStatus)] = [("Assigned", 1), ("Filed", 2), ("InProgress", 3), ("Suspended", 4), ("Concluded", 5), ("Withdrawn", 6)],
        [typeof(LegalActionKind)] = [("Filing", 1), ("Order", 2), ("Seizure", 3), ("Hearing", 4), ("Judgment", 5), ("Fee", 6), ("Expense", 7),
            ("StatusChange", 8), ("Other", 9)],
        [typeof(MessageCampaignStatus)] = [("Draft", 1), ("Scheduled", 2), ("Running", 3), ("Completed", 4), ("Cancelled", 5)],
        [typeof(PriorNoticeStatus)] = [("Pending", 1), ("InProgress", 2), ("Valid", 3), ("Closed", 4)],
        [typeof(CreditReportClaimKind)] = [("Claim", 1), ("Impersonation", 2)],
        [typeof(CreditReportClaimStatus)] = [("InProcess", 1), ("Answered", 2), ("TacitlyAccepted", 3), ("Withdrawn", 4)],
        [typeof(IssuedDocumentKind)] = [("PaymentPlan", 1), ("Statement", 2), ("LoanClearance", 3), ("GeneralClearance", 4),
            ("BalanceCertificate", 5), ("TaxCertificate", 6),
            // Feature 014 (T014; research C27).
            ("AccountStatement", 7), ("AccountBalanceCertificate", 8), ("DepositTaxCertificate", 9), ("WithholdingCertificate", 10),
            ("TermDepositTitle", 11), ("ContributionCertificate", 12), ("ContributionClearance", 13), ("MembershipWithdrawalSettlement", 14),
            ("GarnishmentResponse", 15)],
        // Feature 014, US13 (T194): los títulos del CDAT reemplazados y liquidados.
        [typeof(IssuedDocumentStatus)] = [("Valid", 1), ("SubjectToFinality", 2), ("Voided", 3), ("Superseded", 4), ("Settled", 5)],
        [typeof(IssuedDocumentEventKind)] = [("Issued", 1), ("Delivered", 2), ("Voided", 3), ("Superseded", 4), ("Settled", 5)],
        [typeof(DocumentDeliveryMode)] = [("Download", 1), ("Email", 2)],
        [typeof(RegulatoryFileKind)] = [("Sicses", 1), ("CreditBureau", 2), ("DepositsSicses", 3), ("Exogenous", 4)],
        [typeof(RegulatoryFileStatus)] = [("Validated", 1), ("Generated", 2), ("Superseded", 3), ("Uploaded", 4)],

        // ------------------------------------------------------------------- feature 014: captaciones y aportes (T015) --
        [typeof(LineNature)] = [("Placement", 1), ("Deposit", 2), ("Contribution", 3)],
        [typeof(DepositClass)] = [("Demand", 1), ("Contractual", 2), ("Permanent", 3), ("TermDeposit", 4)],
        [typeof(BalanceMethod)] = [("DailyBalance", 1), ("AverageBalance", 2), ("MinimumBalance", 3)],
        [typeof(RateTierMode)] = [("WholeBalance", 1), ("Stepped", 2)],
        [typeof(InterestTiming)] = [("Arrears", 1), ("Advance", 2)],
        [typeof(RolloverRateRule)] = [("SameRate", 1), ("CurrentTableRate", 2), ("Agreed", 3)],
        [typeof(AfterMaturityTreatment)] = [("StopAccruing", 1), ("MoveToDemand", 2)],
        [typeof(MemberAccountStatus)] = [("Opening", 1), ("Active", 2), ("Inactive", 3), ("Matured", 4), ("Closing", 5), ("Closed", 6)],
        [typeof(MemberAccountPartyRole)] = [("Holder", 1), ("CoHolder", 2), ("Signatory", 3), ("LegalRepresentative", 4), ("Beneficiary", 5)],
        [typeof(HandlingMode)] = [("Indistinct", 1), ("Joint", 2)],
        [typeof(MemberAccountConcept)] = [("Principal", 1), ("InterestPayable", 2), ("Withholding", 3), ("Gmf", 4),
            ("ContributionMinimum", 5), ("ContributionOrdinary", 6), ("ContributionExtraordinary", 7), ("ContributionRevaluation", 8),
            ("ChargeReceivable", 9), ("Bonus", 10), ("Unclaimed", 11)],
        [typeof(MemberAccountTransactionKind)] = [("OpeningBalance", 1), ("Deposit", 2), ("Withdrawal", 3), ("TransferIn", 4),
            ("TransferOut", 5), ("InterestAccrued", 6), ("AccrualAdjustment", 7), ("InterestCredited", 8), ("InterestPaid", 9),
            ("WithholdingApplied", 10), ("GmfCharged", 11), ("ChargeApplied", 12), ("PenaltyApplied", 13), ("Capitalization", 14),
            ("Reclassification", 15), ("InternalOffset", 16), ("Refund", 17), ("ToUnclaimed", 18), ("GarnishmentConsigned", 19),
            ("ChargeWaived", 20), ("BonusRecognized", 21), ("AssemblyCredit", 22)],
        [typeof(GmfExemptionPolicy)] = [("SingleMarkedAccount", 1), ("PerHolderLimit", 2), ("NotApplicable", 3)],
        [typeof(WithholdingThresholdScope)] = [("AllDepositClasses", 1), ("ExceptTermDeposit", 2), ("None", 3)],
        [typeof(HoldReason)] = [("Internal", 1), ("LoanCollateral", 2), ("SignaturePending", 3), ("Deceased", 4), ("ScreeningMatch", 5),
            ("Garnishment", 6), ("Other", 99)],
        [typeof(ChargeKind)] = [("MaintenanceFee", 1), ("ExtraOperation", 2), ("ExtraDocument", 3), ("MediumReplacement", 4),
            ("Inactivity", 5), ("Other", 99)],
        [typeof(MembershipWithdrawalCause)] = [("Voluntary", 1), ("Exclusion", 2), ("Death", 3), ("Other", 99)],
        // Decisión del dueño del 2026-10-18 (D45).
        [typeof(PendingLoanBalanceHandling)] = [("MoveToCashier", 1), ("KeepRepaymentChannel", 2)],
        [typeof(MembershipWithdrawalStatus)] = [("Filed", 1), ("Approved", 2), ("Settled", 3), ("Paid", 4), ("Desisted", 5),
            ("Rejected", 6), ("ToRedo", 7)],
        [typeof(AssemblyDistributionKind)] = [("Revaluation", 1), ("SurplusDistribution", 2), ("Amortization", 3)],
        [typeof(ContributionClass)] = [("Minimum", 1), ("Ordinary", 2), ("Extraordinary", 3), ("Revalued", 4)],
        [typeof(IssueTermBand)] = [("UnderSixMonths", 1), ("SixToTwelve", 2), ("TwelveToEighteen", 3), ("EighteenOrMore", 4)],
        [typeof(AccountFlag)] = [("Standard", 1), ("GmfExempt", 2), ("Garnished", 3)],
        [typeof(MemberAccountOperationKind)] = [("OpeningBalance", 1), ("Deposit", 2), ("Withdrawal", 3), ("Transfer", 4), ("Reversal", 5),
            ("InterestSettlement", 6), ("TermDepositConstitution", 7), ("TermDepositInterestPayment", 8), ("TermDepositRollover", 9),
            ("TermDepositRenewal", 10), ("TermDepositRedemption", 11), ("Cancellation", 12), ("Charge", 13), ("ChargeWaiver", 14),
            ("InternalOffset", 15), ("DirectDebit", 16), ("ContributionRefund", 17), ("AssemblyCredit", 18), ("Reclassification", 19),
            ("ContractualMaturity", 20), ("ToUnclaimed", 21), ("DeceasedDelivery", 22), ("GarnishmentConsignment", 23),
            ("ContractualSetup", 24), ("ContractualBreach", 25), ("ContractualBonus", 26), ("UnclaimedPayment", 27), ("ChargeCollection", 28)],
        [typeof(MemberAccountOrigin)] = [("Opening", 1), ("Migration", 2)],
        [typeof(ContractualStatus)] = [("Current", 1), ("Breached", 2), ("Fulfilled", 3)],
        [typeof(AccountTermsCause)] = [("Constitution", 1), ("Rollover", 2), ("Renewal", 3), ("Migration", 4), ("ContractualSetup", 5),
            ("ContractualBreach", 6), ("AgreedRate", 7), ("RateNegotiation", 8)],
        [typeof(InterestDestination)] = [("ToAccount", 1), ("PaymentOrder", 2), ("Capitalize", 3)],
        [typeof(MaturityInstruction)] = [("Rollover", 1), ("RenewWithNewTerms", 2), ("CancelAndPay", 3)],
        [typeof(EarlyRedemptionPenaltyMode)] = [("PenaltyRate", 1), ("Discount", 2)],
        [typeof(ContractualBreachEffect)] = [("BreachRate", 1), ("MoveToDemand", 2), ("Cancel", 3)],
        [typeof(BonusRecognition)] = [("AtFulfillment", 1), ("Provisioned", 2)],
        [typeof(ContractualMaturityDestinations)] = [("None", 0), ("TransferToDemand", 1), ("DisbursementOrder", 2), ("All", 3)],
        [typeof(ChargeTrigger)] = [("Periodic", 1), ("WithdrawalOverFree", 2), ("DepositOverFree", 3), ("ExtraDocument", 4),
            ("MediumReplacement", 5), ("Inactivity", 6), ("Manual", 7)],
        [typeof(ChargeCalculation)] = [("FixedAmount", 1), ("Percentage", 2)],
        [typeof(ChargeBase)] = [("OperationAmount", 1), ("AverageBalance", 2)],
        [typeof(ChargeShortfallPolicy)] = [("KeepPending", 1), ("Waive", 2)],
        [typeof(AccountChargeStatus)] = [("Collected", 1), ("Pending", 2), ("Waived", 3), ("Reversed", 4)],
        [typeof(ContributionAmountMode)] = [("FixedAmount", 1), ("IncomeFraction", 2), ("MinimumWageFraction", 3)],
        [typeof(ExcessContributionAction)] = [("Reject", 1), ("RefundByDecision", 2)],
        [typeof(ConceptAllocationRule)] = [("Priority", 1), ("Proportional", 2)],
        // Feature 014, US3 (T086, research E58): las solicitudes de devolución y de cesión de aportes.
        [typeof(ContributionRequestKind)] = [("Refund", 1), ("Assignment", 2)],
        [typeof(ContributionRequestStatus)] = [("PendingApproval", 1), ("Approved", 2), ("Rejected", 3)],
        [typeof(ContributionRefundCause)] = [("ParticipationExcess", 1), ("Other", 99)],
        // US9 (T151): el concepto de una fila del acumulado tributario migrado.
        [typeof(MigratedTaxConcept)] = [("Interest", 1), ("Withholding", 2), ("Gmf", 3), ("InflationaryComponent", 4)],
        [typeof(DirectDebitScope)] = [("SingleLoan", 1), ("AllLoansOfHolder", 2)],
        [typeof(PayoutPurpose)] = [("Withdrawal", 1), ("ContributionRefund", 2), ("DeceasedDelivery", 3), ("TermDepositInterest", 4),
            ("TermDepositRedemption", 5), ("GarnishmentConsignment", 6), ("Cancellation", 7), ("SurplusPayment", 8), ("UnclaimedPayment", 9)],
        [typeof(GarnishmentStatus)] = [("Registered", 1), ("Active", 2), ("Released", 3), ("Rejected", 4)],
        // US11 (T187; research E161): la autoridad que embarga y sobre qué cuentas recae.
        [typeof(GarnishmentAuthority)] = [("Court", 1), ("TaxAuthority", 2), ("OtherPublicEntity", 3)],
        [typeof(GarnishmentAccountsScope)] = [("AllSavings", 1), ("Specific", 2)],
        [typeof(GarnishmentResponseKind)] = [("Acknowledgement", 1), ("BalanceReport", 2), ("ContributionsNotGarnishable", 3),
            ("ConsignmentNotice", 4), ("ReleaseAcknowledgement", 5)],
        [typeof(MembershipWithdrawalLineKind)] = [("ContributionBalance", 1), ("SavingsBalance", 2), ("TermDepositBalance", 3),
            ("InterestToDate", 4), ("Withholding", 5), ("Gmf", 6), ("LoanOffset", 7), ("LossDeduction", 8), ("Refund", 9)],
        [typeof(RevaluationBase)] = [("AverageBalance", 1), ("YearEndBalance", 2)],
        [typeof(SurplusDestination)] = [("Contributions", 1), ("Savings", 2), ("Payment", 3)],
        [typeof(AssemblyDistributionStatus)] = [("Simulated", 1), ("Approved", 2), ("Applied", 3), ("Voided", 4), ("Reversed", 5)],
        [typeof(DeliveryRoute)] = [("Beneficiaries", 1), ("WithoutProbate", 2), ("Probate", 3)],
        [typeof(DeceasedCaseStatus)] = [("Registered", 1), ("Approved", 2), ("Delivered", 3), ("Cancelled", 4), ("DeliveryPending", 5)],
        [typeof(UnclaimedSource)] = [("UncashedRefund", 1), ("InactiveBalance", 2), ("UncashedPayout", 3)],
        [typeof(UnclaimedStatus)] = [("Held", 1), ("Claimed", 2)],
        [typeof(LiquidityHoldingKind)] = [("BankDeposit", 1), ("TermInvestment", 2), ("TrustFund", 3), ("GovernmentBond", 4), ("Other", 99)],
        // US6 (T171): el estado de una solicitud de tasa negociada.
        [typeof(NegotiatedRateRequestStatus)] = [("PendingDecision", 1), ("Approved", 2), ("Rejected", 3), ("Withdrawn", 4), ("Expired", 5)],
    };

    public static TheoryData<Type> Enums()
    {
        var datos = new TheoryData<Type>();
        foreach (var tipo in Esperados.Keys) datos.Add(tipo);
        return datos;
    }

    [Theory]
    [MemberData(nameof(Enums))]
    public void Cada_enum_lleva_exactamente_sus_nombres_y_numeros(Type tipo)
    {
        var reales = Enum.GetNames(tipo).Select(n => (Nombre: n, Numero: Convert.ToInt32(Enum.Parse(tipo, n)))).ToList();

        reales.Should().BeEquivalentTo(Esperados[tipo], o => o.WithStrictOrdering(),
            $"{tipo.Name} se guarda por su número y nunca se renumera (C65)");
    }

    [Fact]
    public void Todo_enum_del_espacio_de_Cartera_esta_en_la_lista()
    {
        var delEspacio = typeof(LoanConcept).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.Namespace == typeof(LoanConcept).Namespace)
            .ToList();

        delEspacio.Should().BeEquivalentTo(Esperados.Keys, "un enum nuevo de Domain/Enums/Lending se fija aquí con su numeración");
    }

    [Fact]
    public void Las_entregas_y_la_vigente_son_las_de_R15()
    {
        Enum.GetValues<EntregaDeCartera>().Select(e => (e.ToString(), (int)e)).Should().Equal(
            ("K1", 1), ("K2", 2), ("K3", 3), ("K4", 4), ("K5", 5), ("K6a", 6), ("K6b", 7),
            // Feature 014 (T014; research E06): las de captaciones, al final; salen juntas al cierre de la 014.
            ("K7", 8), ("K8", 9), ("K9", 10), ("K10", 11), ("K11", 12));
        CatalogoDeCartera.EntregaVigente.Should().Be(EntregaDeCartera.K11, "T0969 la subió a K5, T1024 (fase 22) a K6a, T1058 (fase 23) a K6b y la 014 (T225) a K11");
    }

    [Fact]
    public void Los_sujetos_de_decision_y_las_acciones_de_segregacion_son_los_fijados()
    {
        LendingDecisionSubjects.Todos.Should().Equal(
            "CreditApplication", "CreditLimit", "Novelty", "Condonation", "WriteOffBatch", "PortfolioEvaluation", "ImpairmentModelChange",
            "ActivationDifferences", "ValidationFailureException", "RiskPolicyVersion",
            // Feature 014 (T019; research C06): los de captaciones, al final.
            "DepositWithdrawal", "NegotiatedRate", "MembershipWithdrawal", "DeceasedDelivery", "ChargeWaiver", "TermDepositRedemption",
            "ContributionRefund", "AssemblyDistribution", "GarnishmentRegistration");
        LendingDecisionSubjects.DeCaptaciones.Should().Equal(LendingDecisionSubjects.Todos.Skip(10));
        LendingDecisionSubjects.Todos.Should().OnlyContain(s => s.Length <= 40, "SubjectType es nvarchar(40)");

        AccionesDeSegregacion.Todas.Should().Equal(
            "Radicar", "Analizar", "Decidir", "Votar", "Desembolsar", "AnularOrdenDeGiro", "ArmarLoteDeCastigo", "AplicarCastigo",
            "CargarMigracion", "AceptarDiferencias", "Aplicar");
        AccionesDeSegregacion.Todas.Should().OnlyContain(a => a.Length <= 30, "Action es nvarchar(30)");
    }

    /// <summary>
    /// Feature 014 (T015; data-model §1.7, research C13 y E04): los valores agregados al final de dos enums que no son del espacio de Cartera —los
    /// orígenes de campo de los archivos planos y la clase de recaudador del contrato (en Application)—, sin renumerar los anteriores.
    /// </summary>
    [Fact]
    public void Los_valores_agregados_fuera_del_espacio_de_Cartera_quedan_al_final()
    {
        ((int)IngenIA365ERP.Domain.Enums.Core.BankFieldSource.DeductionDate).Should().Be(92);
        ((int)IngenIA365ERP.Domain.Enums.Core.BankFieldSource.AccountNumber).Should().Be(93);
        ((int)IngenIA365ERP.Domain.Enums.Core.BankFieldSource.AccountInstallmentPeriod).Should().Be(94);
        ((int)IngenIA365ERP.Domain.Enums.Core.BankFieldSource.PaymasterConceptCode).Should().Be(95);

        Enum.GetValues<IngenIA365ERP.Application.Common.Integration.Lending.ClaseDeRecaudador>().Select(e => (e.ToString(), (int)e)).Should().Equal(
            ("Receipts", 1), ("Payroll", 2), ("Contributions", 3), ("OwnLoanPayoff", 4), ("Test", 5), ("Treasury", 6));
    }
}
