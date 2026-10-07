using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationLoans</c> (feature 013, K1; data-model §29.3): hecho de preparación; único <c>(tanda, fila)</c>; la llave natural
/// (<c>SourceLoanNumber</c>) la valida el comando entre tandas, no un índice.
/// </summary>
public class MigrationLoanRowConfiguration : IEntityTypeConfiguration<MigrationLoanRow>
{
    public const string Tabla = "LND_MigrationLoans";

    public void Configure(EntityTypeBuilder<MigrationLoanRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceLoanNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.CreditLineCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.OfficePublicId).IsRequired();
        builder.Property(e => e.InvoiceNumber).HasMaxLength(30);
        builder.Property(e => e.DisbursementDate).IsRequired();
        builder.Property(e => e.OriginalAmount).Monto().IsRequired();
        builder.Property(e => e.TermsJson).IsRequired();
        builder.Property(e => e.PrepaymentChoice).HasConversion<int?>();
        builder.Property(e => e.RepaymentChannel).HasConversion<int>().IsRequired();
        builder.Property(e => e.EmployeeIdAtPaymaster).HasMaxLength(30);
        builder.Property(e => e.PromissoryNoteKind).HasConversion<int?>();
        builder.Property(e => e.PromissoryNoteNumber).HasMaxLength(30);
        builder.Property(e => e.CustodianChannelCode).HasMaxLength(20);
        builder.Property(e => e.ExternalNoteId).HasMaxLength(60);
        builder.Property(e => e.CollectionStageCode).HasMaxLength(20);

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PaymasterPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationLoans_Part_Row");
    }
}
