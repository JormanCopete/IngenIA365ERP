using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class VoucherTypeConfiguration : IEntityTypeConfiguration<VoucherType>
{
    public void Configure(EntityTypeBuilder<VoucherType> builder)
    {
        builder.ToTable("ACC_VoucherTypes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_VoucherTypes_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(10);
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(20);
        builder.Property(e => e.DocumentType).HasMaxLength(5);
        builder.Property(e => e.AccountingAccountCode).HasMaxLength(15);
        builder.Property(e => e.UpdatesAccounting).HasDefaultValue(false);
        builder.Property(e => e.EquivalentAccountCode).HasMaxLength(15);
        builder.Property(e => e.RequiresDetail).HasDefaultValue(false);
        builder.Property(e => e.PrintFormat).HasMaxLength(2);
        builder.Property(e => e.CostCenterCode).HasMaxLength(10);
        builder.Property(e => e.ControlSequential).HasDefaultValue(false);
        builder.Property(e => e.BankReconciliationCode).HasMaxLength(5);
        builder.Property(e => e.DebitCredit).HasMaxLength(1);
        builder.Property(e => e.HasValidator).HasDefaultValue(false);
        builder.Property(e => e.ValidatorPort).HasMaxLength(10);
        builder.Property(e => e.AutomaticDetail).HasMaxLength(50);
        builder.Property(e => e.TreasuryRestriction).HasDefaultValue(false);
        builder.Property(e => e.Affects3xMil).HasDefaultValue(false);
        builder.Property(e => e.DocumentControlType).HasMaxLength(2);
        builder.Property(e => e.MoneyLaundering).HasDefaultValue(false);
        builder.Property(e => e.ModuleCode).HasMaxLength(5);
        builder.Property(e => e.EquivalentVoucherCode).HasMaxLength(5);
        builder.Property(e => e.EquivalentDocumentCode).HasMaxLength(5);
        builder.Property(e => e.AccountCode2).HasMaxLength(15);
        builder.Property(e => e.Nature).HasMaxLength(1);
        builder.Property(e => e.ReceiptInvoice).HasMaxLength(1);
        builder.Property(e => e.InvoiceControlCode).HasMaxLength(5);
        builder.Property(e => e.ReturnOverdue).HasMaxLength(1);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_ACC_VoucherTypes_Code");

        // Una sola relacion, por el codigo (clave alterna). Con WithOne() vacio, la navegacion
        // AccountingDocument.VoucherType quedaba fuera y EF le creaba por convencion una segunda
        // relacion con FK sombra VoucherTypeId NOT NULL: todo INSERT que solo llenara el codigo
        // (CreateDocumentCommand, el contabilizador de nomina) violaba esa FK contra base real.
        builder.HasMany(e => e.Documents).WithOne(d => d.VoucherType).HasForeignKey(d => d.VoucherTypeCode).HasPrincipalKey(v => v.Code).OnDelete(DeleteBehavior.Restrict);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
