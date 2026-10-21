using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Migration;

/// <summary>
/// <c>LND_ProvisionalCollections</c> (feature 013, K1; data-model §29.5, C44): hecho con escritura única; únicos
/// <c>(activación, fila)</c> y <c>(factura, referencia contable)</c> sin filtro; <c>OperationId</c> es bigint.
/// </summary>
public class ProvisionalCollectionConfiguration : IEntityTypeConfiguration<ProvisionalCollection>
{
    public const string Tabla = "LND_ProvisionalCollections";

    public void Configure(EntityTypeBuilder<ProvisionalCollection> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.ValueDate).IsRequired();
        builder.Property(e => e.AccountingReference).HasMaxLength(100).IsRequired();

        builder.HasOne<LendingActivation>().WithMany().HasForeignKey(e => e.ActivationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ActivationId, e.RowNumber }).IsUnique()
            .HasDatabaseName("UK_LND_ProvisionalCollections_Activation_Row");
        builder.HasIndex(e => new { e.InvoiceNumber, e.AccountingReference }).IsUnique()
            .HasDatabaseName("UK_LND_ProvisionalCollections_Invoice_Reference");
    }
}
