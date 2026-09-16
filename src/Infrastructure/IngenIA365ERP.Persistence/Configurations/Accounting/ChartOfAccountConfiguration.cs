using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class ChartOfAccountConfiguration : IEntityTypeConfiguration<ChartOfAccount>
{
    public void Configure(EntityTypeBuilder<ChartOfAccount> builder)
    {
        builder.ToTable("ACC_ChartOfAccounts");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_ChartOfAccounts_PublicId");

        builder.Property(e => e.Code).HasMaxLength(12).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Nature).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.NiifItemCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Origin).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.BankAccountNumber).HasMaxLength(30);
        builder.Property(e => e.TaxKind).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.TaxConceptCode).HasMaxLength(20);

        // Único sólo entre no eliminadas (feature 009, U1): reiniciar con otro catálogo hace
        // soft-delete de las cuentas del catálogo anterior y vuelve a copiar; recrear un código
        // eliminado también debe poder insertar. Sin el filtro, ambas cosas reventaban en la base.
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_ACC_ChartOfAccounts_Code").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.ParentId).HasDatabaseName("IX_ACC_ChartOfAccounts_Parent");
        builder.HasIndex(e => new { e.IsMovement, e.IsActive }).HasDatabaseName("IX_ACC_ChartOfAccounts_Movement_Active");
        builder.HasIndex(e => e.BankId).HasDatabaseName("IX_ACC_ChartOfAccounts_Bank").HasFilter("[BankId] IS NOT NULL");

        builder.HasOne(e => e.Parent).WithMany(p => p.Children).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Bank).WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.TaxRates).WithOne(r => r.Account).HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(e => e.EsBancaria);
        builder.Ignore(e => e.EsDeImpuesto);
    }
}

public class AccountTaxRateConfiguration : IEntityTypeConfiguration<AccountTaxRate>
{
    public void Configure(EntityTypeBuilder<AccountTaxRate> builder)
    {
        builder.ToTable("ACC_AccountTaxRates");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_AccountTaxRates_PublicId");
        builder.Property(e => e.Rate).HasPrecision(9, 4);
        builder.HasIndex(e => new { e.AccountId, e.ValidFrom }).IsUnique().HasDatabaseName("UK_ACC_AccountTaxRates_Account_ValidFrom").HasFilter("[IsDeleted] = 0");
    }
}

public class VoucherTypeConfiguration : IEntityTypeConfiguration<VoucherType>
{
    public void Configure(EntityTypeBuilder<VoucherType> builder)
    {
        builder.ToTable("ACC_VoucherTypes");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_VoucherTypes_PublicId");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Usage).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.ModuleCode).HasMaxLength(3);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_ACC_VoucherTypes_Code").HasFilter("[IsDeleted] = 0");
    }
}

public class CrossDocumentTypeConfiguration : IEntityTypeConfiguration<CrossDocumentType>
{
    public void Configure(EntityTypeBuilder<CrossDocumentType> builder)
    {
        builder.ToTable("ACC_CrossDocumentTypes");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_CrossDocumentTypes_PublicId");
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_ACC_CrossDocumentTypes_Code").HasFilter("[IsDeleted] = 0");
    }
}
