using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Operations;

/// <summary>
/// <c>LND_DisbursementOrders</c> (feature 013; data-model §7.4): una orden por tramo, nunca en especie, neto positivo igual al bruto
/// menos lo recogido, archivo sólo en transferencias; índice de las pendientes de llevar a un archivo.
/// </summary>
public class DisbursementOrderConfiguration : IEntityTypeConfiguration<DisbursementOrder>
{
    public const string Tabla = "LND_DisbursementOrders";

    public void Configure(EntityTypeBuilder<DisbursementOrder> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_DisbursementOrders_Form", "[Form] <> 4");
            t.HasCheckConstraint("CK_LND_DisbursementOrders_Net", "[NetAmount] = [GrossAmount] - [PayoffAmount] AND [NetAmount] > 0");
            t.HasCheckConstraint("CK_LND_DisbursementOrders_PaidByFile", "[PaidByFile] = 0 OR [Form] = 1");
        });

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Form).HasConversion<int>().IsRequired();
        builder.Property(e => e.BeneficiaryTaxIdType).HasMaxLength(10).IsRequired();
        builder.Property(e => e.BeneficiaryTaxId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.BeneficiaryName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.DestinationAccountNumber).HasMaxLength(25);
        builder.Property(e => e.GrossAmount).Monto().IsRequired();
        builder.Property(e => e.PayoffAmount).Monto().IsRequired();
        builder.Property(e => e.NetAmount).Monto().IsRequired();
        builder.Property(e => e.ScheduledDate).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.IssuedAt).IsRequired();
        builder.Property(e => e.ResultDueOn).IsRequired();
        builder.Property(e => e.VoidReason).HasMaxLength(500);
        builder.Property(e => e.PaidByFile).IsRequired();

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.DisbursementOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.BeneficiaryPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.DestinationBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.VoidRequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DisbursementOrder>().WithMany().HasForeignKey(e => e.PreviousOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DisbursementFile>().WithMany().HasForeignKey(e => e.CurrentDisbursementFileId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.DisbursementOperationId).IsUnique().HasDatabaseName("UK_LND_DisbursementOrders_Operation");
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_DisbursementOrders_Number");
        builder.HasIndex(e => new { e.Status, e.ResultDueOn }).HasDatabaseName("IX_LND_DisbursementOrders_Status_ResultDueOn");
        builder.HasIndex(e => e.Status, "IX_LND_DisbursementOrders_PendingFile")
            .HasFilter("[PaidByFile] = 1 AND [CurrentDisbursementFileId] IS NULL");
    }
}
