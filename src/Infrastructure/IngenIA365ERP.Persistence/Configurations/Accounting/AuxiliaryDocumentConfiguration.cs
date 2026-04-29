using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class AuxiliaryDocumentConfiguration : IEntityTypeConfiguration<AuxiliaryDocument>
{
    public void Configure(EntityTypeBuilder<AuxiliaryDocument> builder)
    {
        builder.ToTable("ACC_AuxiliaryDocuments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_AuxiliaryDocuments_PublicId");

        builder.Property(e => e.LegacyKey).HasMaxLength(100);
        builder.Property(e => e.AuxiliaryType).HasMaxLength(5).IsRequired();
        builder.Property(e => e.DocumentType).HasMaxLength(5);
        builder.Property(e => e.DocumentNumber).HasMaxLength(20);
        builder.Property(e => e.Detail).HasMaxLength(200);
        builder.Property(e => e.InvoiceNumber).HasMaxLength(20);
        builder.Property(e => e.OriginalValue).HasPrecision(18, 2);
        builder.Property(e => e.InitialBalance).HasPrecision(18, 2);
        builder.Property(e => e.JanDebit).HasPrecision(18, 2);
        builder.Property(e => e.JanCredit).HasPrecision(18, 2);
        builder.Property(e => e.FebDebit).HasPrecision(18, 2);
        builder.Property(e => e.FebCredit).HasPrecision(18, 2);
        builder.Property(e => e.MarDebit).HasPrecision(18, 2);
        builder.Property(e => e.MarCredit).HasPrecision(18, 2);
        builder.Property(e => e.AprDebit).HasPrecision(18, 2);
        builder.Property(e => e.AprCredit).HasPrecision(18, 2);
        builder.Property(e => e.MayDebit).HasPrecision(18, 2);
        builder.Property(e => e.MayCredit).HasPrecision(18, 2);
        builder.Property(e => e.JunDebit).HasPrecision(18, 2);
        builder.Property(e => e.JunCredit).HasPrecision(18, 2);
        builder.Property(e => e.JulDebit).HasPrecision(18, 2);
        builder.Property(e => e.JulCredit).HasPrecision(18, 2);
        builder.Property(e => e.AugDebit).HasPrecision(18, 2);
        builder.Property(e => e.AugCredit).HasPrecision(18, 2);
        builder.Property(e => e.SepDebit).HasPrecision(18, 2);
        builder.Property(e => e.SepCredit).HasPrecision(18, 2);
        builder.Property(e => e.OctDebit).HasPrecision(18, 2);
        builder.Property(e => e.OctCredit).HasPrecision(18, 2);
        builder.Property(e => e.NovDebit).HasPrecision(18, 2);
        builder.Property(e => e.NovCredit).HasPrecision(18, 2);
        builder.Property(e => e.DecDebit).HasPrecision(18, 2);
        builder.Property(e => e.DecCredit).HasPrecision(18, 2);
        builder.Property(e => e.Period13Amount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
