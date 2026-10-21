using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_MigrationSaleReconciliations</c> (feature 013, K1; data-model §21.5): único <c>(carga, versión, pago de crédito)</c> sin
/// filtro; índices de búsqueda por mensaje y por factura; FK a las filas de preparación, a la diferencia y al crédito resultante.
/// </summary>
public class MigrationSaleReconciliationConfiguration : IEntityTypeConfiguration<MigrationSaleReconciliation>
{
    public const string Tabla = "LND_MigrationSaleReconciliations";

    public void Configure(EntityTypeBuilder<MigrationSaleReconciliation> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.CheckVersion).IsRequired();
        builder.Property(e => e.MessagePublicId).IsRequired();
        builder.Property(e => e.SaleDocumentPublicId).IsRequired();
        builder.Property(e => e.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.CreditPaymentPublicId).IsRequired();
        builder.Property(e => e.SaleDate).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>().IsRequired();
        builder.Property(e => e.MarkedFromList).IsRequired();
        builder.Property(e => e.MarkReason).HasMaxLength(500);
        builder.Property(e => e.AdjustmentsUpToCutoff).IsRequired();
        builder.Property(e => e.AdjustmentsAfterCutoff).IsRequired();

        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoanRow>().WithMany().HasForeignKey(e => e.MigrationLoanRowId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationSaleRow>().WithMany().HasForeignKey(e => e.MigrationSaleRowId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.MarkedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoadDifference>().WithMany().HasForeignKey(e => e.DifferenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.ResultingLoanId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MigrationLoadId, e.CheckVersion, e.CreditPaymentPublicId }).IsUnique()
            .HasDatabaseName("UK_LND_MigrationSaleReconciliations_Load_Ver_Payment");
        builder.HasIndex(e => e.MessagePublicId).HasDatabaseName("IX_LND_MigrationSaleReconciliations_Message");
        builder.HasIndex(e => e.InvoiceNumber).HasDatabaseName("IX_LND_MigrationSaleReconciliations_Invoice");
    }
}
