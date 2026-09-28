using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_CashDocumentLines</c> (feature 012, I3, T583; FR-099; data-model §15): las líneas del documento de diferencia de arqueo,
/// únicas <c>(DocumentId, LineNumber)</c> entre vivas; valor en pesos; FK <c>Restrict</c> a la línea del arqueo, al medio, al
/// usuario cajero y a su persona.
/// </summary>
public class CashDocumentLineConfiguration : IEntityTypeConfiguration<CashDocumentLine>
{
    public void Configure(EntityTypeBuilder<CashDocumentLine> builder)
    {
        builder.ComoEntidadDeInventario("INV_CashDocumentLines");

        builder.Property(e => e.Sign).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Treatment).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.HasOne(e => e.Document).WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashCountLine>().WithMany().HasForeignKey(e => e.CashCountLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CashierUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.CashierPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DocumentId, e.LineNumber }).IsUnique()
            .HasDatabaseName("UK_INV_CashDocumentLines_Document_LineNumber").HasFilter("[IsDeleted] = 0");
    }
}
