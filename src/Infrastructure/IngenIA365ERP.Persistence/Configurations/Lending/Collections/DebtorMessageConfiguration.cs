using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Collections;

/// <summary>
/// <c>LND_DebtorMessages</c> (feature 013, C.3; data-model §26.1): hecho. Único <c>IdempotencyKey</c>; canal, adaptador y plantilla
/// sellados. K4 (T0939) suma <c>CampaignId</c> con su índice filtrado.
/// </summary>
public class DebtorMessageConfiguration : IEntityTypeConfiguration<DebtorMessage>
{
    public const string Tabla = "LND_DebtorMessages";

    public void Configure(EntityTypeBuilder<DebtorMessage> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RecipientRole).HasConversion<int>().IsRequired();
        builder.Property(e => e.Purpose).HasConversion<int>().IsRequired();
        builder.Property(e => e.Channel).HasConversion<int>().IsRequired();
        builder.Property(e => e.ChannelCode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(300);
        builder.Property(e => e.RenderedBody).IsRequired();
        builder.Property(e => e.BodySha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(e => e.OriginKind).HasMaxLength(30).IsRequired();
        builder.Property(e => e.IsManualImmediate).IsRequired();
        builder.Property(e => e.RequestedAt).IsRequired();
        builder.Property(e => e.RequestedByKind).HasConversion<int>().IsRequired();

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NotificationTemplate>().WithMany().HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PriorNotice>().WithMany().HasForeignKey(e => e.PriorNoticeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IssuedLendingDocument>().WithMany().HasForeignKey(e => e.IssuedDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstallmentNonDeduction>().WithMany().HasForeignKey(e => e.InstallmentNonDeductionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MessageCampaign>().WithMany().HasForeignKey(e => e.CampaignId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName("UK_LND_DebtorMessages_IdempotencyKey");
        builder.HasIndex(e => new { e.PersonId, e.RequestedAt }).HasDatabaseName("IX_LND_DebtorMessages_PersonId_RequestedAt");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_DebtorMessages_LoanId");
        builder.HasIndex(e => e.CampaignId).HasDatabaseName("IX_LND_DebtorMessages_CampaignId").HasFilter("[CampaignId] IS NOT NULL");
    }
}
