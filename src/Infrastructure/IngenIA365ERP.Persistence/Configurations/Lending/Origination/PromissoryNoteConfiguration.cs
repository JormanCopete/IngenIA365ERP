using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary><c>LND_PromissoryNotes</c> (feature 013; data-model §2.9): el id externo del depósito es único cuando existe.</summary>
public class PromissoryNoteConfiguration : IEntityTypeConfiguration<PromissoryNote>
{
    public const string Tabla = "LND_PromissoryNotes";

    public void Configure(EntityTypeBuilder<PromissoryNote> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Number).HasMaxLength(40);
        builder.Property(e => e.IssuedOn).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.CustodianChannelCode).HasMaxLength(20);
        builder.Property(e => e.ExternalNoteId).HasMaxLength(80);
        builder.Property(e => e.IsSimulated).IsRequired();
        builder.Property(e => e.EndorseeName).HasMaxLength(200);
        builder.Property(e => e.PhysicalLocation).HasMaxLength(120);
        builder.Property(e => e.InstructionsAttachmentPublicId);
        builder.Property(e => e.DocumentAttachmentPublicId);
        builder.Property(e => e.VoidReason).HasMaxLength(300);
        builder.Property(e => e.ReleasedOn);
        builder.Property(e => e.LastSyncedAt);

        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ExternalNoteId).IsUnique()
            .HasDatabaseName("UK_LND_PromissoryNotes_ExternalNoteId").HasFilter("[ExternalNoteId] IS NOT NULL");
    }
}
