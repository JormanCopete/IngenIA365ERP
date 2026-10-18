using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Collections;

// Feature 013, K4 (T0939; data-model §26.3–§26.10): las once tablas de cobranza de CarteraCobranza. FK Restrict, PublicId único y filtro de
// borrado por ComoEntidadDeCartera; los únicos de los hechos no filtran por borrado (data-model §0.1).

/// <summary><c>LND_MessageCampaigns</c>: código único entre las vivas; la plantilla, por FK.</summary>
public class MessageCampaignConfiguration : IEntityTypeConfiguration<MessageCampaign>
{
    public const string Tabla = "LND_MessageCampaigns";

    public void Configure(EntityTypeBuilder<MessageCampaign> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(MessageCampaign.LargoDelCodigo).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Purpose).HasConversion<int>().IsRequired();
        builder.Property(e => e.Channel).HasConversion<int>().IsRequired();
        builder.Property(e => e.SelectionCriteriaJson).IsRequired();
        builder.Property(e => e.ScheduledFor).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.TargetCount).IsRequired();
        builder.Property(e => e.QueuedCount).IsRequired();
        builder.Property(e => e.ExcludedCount).IsRequired();
        builder.Property(e => e.CancelReason).HasMaxLength(500);

        builder.HasOne<NotificationTemplate>().WithMany().HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ScheduledByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_LND_MessageCampaigns_Code").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.Status, e.ScheduledFor }).HasDatabaseName("IX_LND_MessageCampaigns_Status_ScheduledFor");
        builder.HasIndex(e => e.TemplateId).HasDatabaseName("IX_LND_MessageCampaigns_TemplateId");
        builder.HasIndex(e => e.CreatedByUserId).HasDatabaseName("IX_LND_MessageCampaigns_CreatedByUserId");
        builder.HasIndex(e => e.ScheduledByUserId).HasDatabaseName("IX_LND_MessageCampaigns_ScheduledByUserId");
        builder.HasIndex(e => e.CancelledByUserId).HasDatabaseName("IX_LND_MessageCampaigns_CancelledByUserId");
    }
}

/// <summary><c>LND_CollectionStages</c>: <c>UK (Code, ValidFrom)</c> entre las vivas.</summary>
public class CollectionStageConfiguration : IEntityTypeConfiguration<CollectionStage>
{
    public const string Tabla = "LND_CollectionStages";

    public void Configure(EntityTypeBuilder<CollectionStage> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(CollectionStage.LargoDelCodigo).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Order).IsRequired();
        builder.Property(e => e.MinDaysPastDue).IsRequired();
        builder.Property(e => e.IsLegal).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();

        builder.HasIndex(e => new { e.Code, e.ValidFrom }).IsUnique().HasDatabaseName("UK_LND_CollectionStages_Code_ValidFrom")
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.ValidFrom, e.ValidTo }).HasDatabaseName("IX_LND_CollectionStages_Validity");
    }
}

/// <summary>
/// <c>LND_CollectionStageHistory</c>: hecho; <c>(LoanId, EnteredOn)</c>. La etapa migrada se registra una sola vez por crédito (C64): único
/// filtrado a <c>Source = 'Migration'</c>, sin filtro de borrado porque un hecho no se da de baja.
/// </summary>
public class CollectionStageHistoryEntryConfiguration : IEntityTypeConfiguration<CollectionStageHistoryEntry>
{
    public const string Tabla = "LND_CollectionStageHistory";

    public void Configure(EntityTypeBuilder<CollectionStageHistoryEntry> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.StageCode).HasMaxLength(CollectionStage.LargoDelCodigo).IsRequired();
        builder.Property(e => e.EnteredOn).IsRequired();
        builder.Property(e => e.DaysPastDueAtEntry).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(300);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CollectionStage>().WithMany().HasForeignKey(e => e.CollectionStageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.ProcessRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CollectionStageHistoryEntry>().WithMany().HasForeignKey(e => e.PreviousEntryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanId, e.EnteredOn }).HasDatabaseName("IX_LND_CollectionStageHistory_Loan_EnteredOn");
        builder.HasIndex(e => e.LoanId).IsUnique().HasDatabaseName("UK_LND_CollectionStageHistory_Migration")
            .HasFilter("[Source] = 'Migration'");
        builder.HasIndex(e => e.CollectionStageId).HasDatabaseName("IX_LND_CollectionStageHistory_CollectionStageId");
        builder.HasIndex(e => e.ProcessRunId).HasDatabaseName("IX_LND_CollectionStageHistory_ProcessRunId");
        builder.HasIndex(e => e.ActorUserId).HasDatabaseName("IX_LND_CollectionStageHistory_ActorUserId");
        builder.HasIndex(e => e.PreviousEntryId).HasDatabaseName("IX_LND_CollectionStageHistory_PreviousEntryId");
    }
}

/// <summary><c>LND_CollectorAssignmentRules</c>: por prioridad y vigencia; línea y pagaduría por FK.</summary>
public class CollectorAssignmentRuleConfiguration : IEntityTypeConfiguration<CollectorAssignmentRule>
{
    public const string Tabla = "LND_CollectorAssignmentRules";

    public void Configure(EntityTypeBuilder<CollectorAssignmentRule> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Priority).IsRequired();
        builder.Property(e => e.StageCode).HasMaxLength(CollectionStage.LargoDelCodigo);
        builder.Property(e => e.MinBalance).Monto();
        builder.Property(e => e.MaxBalance).Monto();
        builder.Property(e => e.Distribution).HasConversion<int>().IsRequired();
        builder.Property(e => e.AssignWholeDebtor).IsRequired();
        builder.Property(e => e.CollectorsJson).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();

        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PaymasterPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Priority, e.ValidFrom }).HasDatabaseName("IX_LND_CollectorAssignmentRules_Priority_ValidFrom");
        builder.HasIndex(e => e.CreditLineId).HasDatabaseName("IX_LND_CollectorAssignmentRules_CreditLineId");
        builder.HasIndex(e => e.PaymasterPersonId).HasDatabaseName("IX_LND_CollectorAssignmentRules_PaymasterPersonId");
    }
}

/// <summary><c>LND_CollectorAssignments</c>: hecho; <c>(LoanId, AssignedOn)</c>, <c>(PersonId, AssignedOn)</c> y <c>(CollectorUserId, AssignedOn)</c>.</summary>
public class CollectorAssignmentConfiguration : IEntityTypeConfiguration<CollectorAssignment>
{
    public const string Tabla = "LND_CollectorAssignments";

    public void Configure(EntityTypeBuilder<CollectorAssignment> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.AssignedOn).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(300);
        builder.Ignore(e => e.EsManual);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CollectorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CollectorAssignmentRule>().WithMany().HasForeignKey(e => e.CollectorAssignmentRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CollectorAssignment>().WithMany().HasForeignKey(e => e.PreviousAssignmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanId, e.AssignedOn }).HasDatabaseName("IX_LND_CollectorAssignments_Loan_AssignedOn");
        builder.HasIndex(e => new { e.PersonId, e.AssignedOn }).HasDatabaseName("IX_LND_CollectorAssignments_Person_AssignedOn");
        builder.HasIndex(e => new { e.CollectorUserId, e.AssignedOn }).HasDatabaseName("IX_LND_CollectorAssignments_Collector_AssignedOn");
        builder.HasIndex(e => e.CollectorAssignmentRuleId).HasDatabaseName("IX_LND_CollectorAssignments_RuleId");
        builder.HasIndex(e => e.ActorUserId).HasDatabaseName("IX_LND_CollectorAssignments_ActorUserId");
        builder.HasIndex(e => e.PreviousAssignmentId).HasDatabaseName("IX_LND_CollectorAssignments_PreviousAssignmentId");
    }
}

/// <summary>
/// <c>LND_CollectionActions</c>: hecho; <c>(LoanId, OccurredAt)</c> y <c>(PersonId, OccurredAt)</c> (la historia de la semana). Una gestión
/// se anula una sola vez: único filtrado de <c>VoidsActionId</c>.
/// </summary>
public class CollectionActionConfiguration : IEntityTypeConfiguration<CollectionAction>
{
    public const string Tabla = "LND_CollectionActions";

    public void Configure(EntityTypeBuilder<CollectionAction> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.ContactRole).HasConversion<int>().IsRequired();
        builder.Property(e => e.Channel).HasConversion<int>().IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.IsDirectContact).IsRequired();
        builder.Property(e => e.Result).HasConversion<int>().IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(CollectionAction.LargoDeLasNotas);
        builder.Property(e => e.VoidReason).HasMaxLength(500);
        builder.Property(e => e.WindowDecision).HasMaxLength(20);
        builder.Property(e => e.WindowReasonCodes).HasMaxLength(300);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CollectorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DebtorMessage>().WithMany().HasForeignKey(e => e.DebtorMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CollectionAction>().WithMany().HasForeignKey(e => e.VoidsActionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanId, e.OccurredAt }).HasDatabaseName("IX_LND_CollectionActions_Loan_OccurredAt");
        builder.HasIndex(e => new { e.PersonId, e.OccurredAt }).HasDatabaseName("IX_LND_CollectionActions_Person_OccurredAt");
        builder.HasIndex(e => e.CollectorUserId).HasDatabaseName("IX_LND_CollectionActions_CollectorUserId");
        builder.HasIndex(e => e.DebtorMessageId).HasDatabaseName("IX_LND_CollectionActions_DebtorMessageId");
        builder.HasIndex(e => e.VoidsActionId).IsUnique().HasDatabaseName("UK_LND_CollectionActions_VoidsActionId")
            .HasFilter("[VoidsActionId] IS NOT NULL");
    }
}

/// <summary><c>LND_PaymentPromises</c>: hecho; <c>UK (CollectionActionId)</c>; <c>(LoanId, PromisedDate)</c>.</summary>
public class PaymentPromiseConfiguration : IEntityTypeConfiguration<PaymentPromise>
{
    public const string Tabla = "LND_PaymentPromises";

    public void Configure(EntityTypeBuilder<PaymentPromise> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.PromisedDate).IsRequired();
        builder.Property(e => e.PromisedAmount).Monto().IsRequired();
        builder.Property(e => e.ToleranceAmount).Monto().IsRequired();

        builder.HasOne<CollectionAction>().WithMany().HasForeignKey(e => e.CollectionActionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CollectionActionId).IsUnique().HasDatabaseName("UK_LND_PaymentPromises_CollectionActionId");
        builder.HasIndex(e => new { e.LoanId, e.PromisedDate }).HasDatabaseName("IX_LND_PaymentPromises_Loan_PromisedDate");
    }
}

/// <summary><c>LND_PaymentAgreements</c>: número único; sus cuotas por navegación.</summary>
public class PaymentAgreementConfiguration : IEntityTypeConfiguration<PaymentAgreement>
{
    public const string Tabla = "LND_PaymentAgreements";

    public void Configure(EntityTypeBuilder<PaymentAgreement> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.PastDueAmountAtAgreement).Monto().IsRequired();
        builder.Property(e => e.TotalAmount).Monto().IsRequired();
        builder.Property(e => e.StartDate).IsRequired();
        builder.Property(e => e.InstallmentCount).IsRequired();
        builder.Property(e => e.RejectionReason).HasMaxLength(500);
        builder.Property(e => e.CancelReason).HasMaxLength(500);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Installments).WithOne().HasForeignKey(c => c.PaymentAgreementId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_PaymentAgreements_Number");
        builder.HasIndex(e => new { e.LoanId, e.Status }).HasDatabaseName("IX_LND_PaymentAgreements_Loan_Status");
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_LND_PaymentAgreements_PersonId");
        builder.HasIndex(e => e.CreatedByUserId).HasDatabaseName("IX_LND_PaymentAgreements_CreatedByUserId");
        builder.HasIndex(e => e.ApprovedByUserId).HasDatabaseName("IX_LND_PaymentAgreements_ApprovedByUserId");
    }
}

/// <summary><c>LND_PaymentAgreementInstallments</c>: hecho; <c>UK (PaymentAgreementId, Number)</c>.</summary>
public class PaymentAgreementInstallmentConfiguration : IEntityTypeConfiguration<PaymentAgreementInstallment>
{
    public const string Tabla = "LND_PaymentAgreementInstallments";

    public void Configure(EntityTypeBuilder<PaymentAgreementInstallment> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).IsRequired();
        builder.Property(e => e.DueDate).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();

        builder.HasIndex(e => new { e.PaymentAgreementId, e.Number }).IsUnique().HasDatabaseName("UK_LND_PaymentAgreementInstallments_Agreement_Number");
    }
}

/// <summary><c>LND_LegalProcesses</c>: número único; abogado y demandado son personas del maestro; la insolvencia que lo suspendió.</summary>
public class LegalProcessConfiguration : IEntityTypeConfiguration<LegalProcess>
{
    public const string Tabla = "LND_LegalProcesses";

    public void Configure(EntityTypeBuilder<LegalProcess> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.AssignedOn).IsRequired();
        builder.Property(e => e.Authority).HasMaxLength(150);
        builder.Property(e => e.CaseNumber).HasMaxLength(60);
        builder.Property(e => e.ProcessType).HasMaxLength(60);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.CloseReason).HasMaxLength(500);
        builder.Ignore(e => e.Abierto);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.LawyerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InsolvencyProceeding>().WithMany().HasForeignKey(e => e.SuspendedByInsolvencyId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_LegalProcesses_Number");
        builder.HasIndex(e => new { e.LoanId, e.Status }).HasDatabaseName("IX_LND_LegalProcesses_Loan_Status");
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_LND_LegalProcesses_PersonId");
        builder.HasIndex(e => e.LawyerPersonId).HasDatabaseName("IX_LND_LegalProcesses_LawyerPersonId");
        builder.HasIndex(e => e.SuspendedByInsolvencyId).HasDatabaseName("IX_LND_LegalProcesses_SuspendedByInsolvencyId");
    }
}

/// <summary><c>LND_LegalProcessActions</c>: hecho; <c>(LegalProcessId, ActionDate)</c>; la operación <c>Charge</c>, una vez.</summary>
public class LegalProcessActionConfiguration : IEntityTypeConfiguration<LegalProcessAction>
{
    public const string Tabla = "LND_LegalProcessActions";

    public void Configure(EntityTypeBuilder<LegalProcessAction> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.ActionDate).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.ChargeableToDebtor).IsRequired();

        builder.HasOne<LegalProcess>().WithMany().HasForeignKey(e => e.LegalProcessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.ChargeOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LegalProcessId, e.ActionDate }).HasDatabaseName("IX_LND_LegalProcessActions_Process_ActionDate");
        builder.HasIndex(e => e.ChargeOperationId).IsUnique().HasDatabaseName("UK_LND_LegalProcessActions_ChargeOperationId")
            .HasFilter("[ChargeOperationId] IS NOT NULL");
        builder.HasIndex(e => e.RecordedByUserId).HasDatabaseName("IX_LND_LegalProcessActions_RecordedByUserId");
    }
}
