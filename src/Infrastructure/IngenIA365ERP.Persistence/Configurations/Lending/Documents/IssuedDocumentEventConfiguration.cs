using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Documents;

/// <summary>
/// <c>LND_IssuedDocumentEvents</c> (feature 013, C.3; data-model §27.3): hecho. Una sola anulación por documento
/// (<c>UK_LND_IssuedDocumentEvents_Voided</c>, filtrado <c>[Kind] = 3</c>).
/// </summary>
public class IssuedDocumentEventConfiguration : IEntityTypeConfiguration<IssuedDocumentEvent>
{
    public const string Tabla = "LND_IssuedDocumentEvents";

    public void Configure(EntityTypeBuilder<IssuedDocumentEvent> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.ActorKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);

        builder.HasOne<IssuedLendingDocument>().WithMany().HasForeignKey(e => e.IssuedDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.CausingOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DebtorMessage>().WithMany().HasForeignKey(e => e.DebtorMessageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.IssuedDocumentId).IsUnique()
            .HasDatabaseName("UK_LND_IssuedDocumentEvents_Voided").HasFilter("[Kind] = 3");
    }
}
