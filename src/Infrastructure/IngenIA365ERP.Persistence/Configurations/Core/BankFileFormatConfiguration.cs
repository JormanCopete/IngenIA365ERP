using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

/// <summary>Formato de archivo bancario (feature 010, N4; D-42): dato con vigencia ligado al banco, compartido por los módulos.</summary>
public class BankFileFormatConfiguration : IEntityTypeConfiguration<BankFileFormat>
{
    public void Configure(EntityTypeBuilder<BankFileFormat> builder)
    {
        builder.ToTable("COR_BankFileFormats");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_BankFileFormats_PublicId");

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UK_COR_BankFileFormats_Code");
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Delimiter).HasMaxLength(5);
        builder.Property(e => e.Encoding).HasMaxLength(20).IsRequired();
        builder.Property(e => e.FileNamePattern).HasMaxLength(120).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.AgreementCode).HasMaxLength(20);
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasIndex(e => new { e.BankId, e.Scope, e.ValidFrom }).HasDatabaseName("IX_COR_BankFileFormats_Bank_Scope_ValidFrom");

        builder.HasOne(e => e.Bank).WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Fields).WithOne(f => f.Format).HasForeignKey(f => f.FormatId).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class BankFileFormatFieldConfiguration : IEntityTypeConfiguration<BankFileFormatField>
{
    public void Configure(EntityTypeBuilder<BankFileFormatField> builder)
    {
        builder.ToTable("COR_BankFileFormatFields");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_BankFileFormatFields_PublicId");

        builder.Property(e => e.Name).HasMaxLength(60).IsRequired();
        builder.Property(e => e.ConstantValue).HasMaxLength(120);
        builder.Property(e => e.PadChar).HasMaxLength(1).IsRequired();
        builder.Property(e => e.ValueFormat).HasMaxLength(40);
        builder.Property(e => e.ValueMapJson).HasMaxLength(400);

        builder.HasIndex(e => new { e.FormatId, e.Record, e.Order }).IsUnique().HasDatabaseName("UK_COR_BankFileFormatFields_Format_Record_Order");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
