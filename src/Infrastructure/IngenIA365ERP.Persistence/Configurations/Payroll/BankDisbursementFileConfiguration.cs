using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>Archivo de dispersión bancaria (feature 010, US8; data-model §2.10): inmutable, uno o varios por corrida aprobada.</summary>
public class BankDisbursementFileConfiguration : IEntityTypeConfiguration<BankDisbursementFile>
{
    public void Configure(EntityTypeBuilder<BankDisbursementFile> builder)
    {
        builder.ToTable("PAY_BankDisbursementFiles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_BankDisbursementFiles_PublicId");

        builder.Property(e => e.FormatCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.GeneratedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(60);
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(30);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 2);
        builder.Property(e => e.FileName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FileSha256).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SentBy).HasMaxLength(100);
        builder.Property(e => e.BankReference).HasMaxLength(60);
        builder.Property(e => e.SentNotes).HasMaxLength(300);
        builder.Property(e => e.VoidedBy).HasMaxLength(100);
        builder.Property(e => e.VoidReason).HasMaxLength(300);

        builder.HasIndex(e => new { e.PayrollRunId, e.Status }).HasDatabaseName("IX_PAY_BankDisbursementFiles_Run_Status");
        builder.HasIndex(e => new { e.PaymentDate, e.Sequence }).HasDatabaseName("IX_PAY_BankDisbursementFiles_PaymentDate_Sequence");

        builder.HasOne(e => e.PayrollRun).WithMany().HasForeignKey(e => e.PayrollRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Bank).WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Format).WithMany().HasForeignKey(e => e.FormatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Lines).WithOne(l => l.File).HasForeignKey(l => l.FileId).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class BankDisbursementFileLineConfiguration : IEntityTypeConfiguration<BankDisbursementFileLine>
{
    public void Configure(EntityTypeBuilder<BankDisbursementFileLine> builder)
    {
        builder.ToTable("PAY_BankDisbursementFileLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_BankDisbursementFileLines_PublicId");

        builder.Property(e => e.AccountNumber).HasMaxLength(25);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.RecordText).HasMaxLength(600).IsRequired();

        builder.HasIndex(e => new { e.FileId, e.LineNumber }).IsUnique().HasDatabaseName("UK_PAY_BankDisbursementFileLines_File_Line");
        builder.HasIndex(e => e.PayrollRunEmployeeId).HasDatabaseName("IX_PAY_BankDisbursementFileLines_RunEmployee");

        builder.HasOne(e => e.RunEmployee).WithMany().HasForeignKey(e => e.PayrollRunEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Payment).WithMany().HasForeignKey(e => e.PayrollPaymentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
