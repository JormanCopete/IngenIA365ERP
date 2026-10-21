using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary><c>LND_CreditApplicationDocuments</c> (feature 013; data-model §2.3): el adjunto va por <c>PublicId</c>, sin FK.</summary>
public class CreditApplicationDocumentConfiguration : IEntityTypeConfiguration<CreditApplicationDocument>
{
    public const string Tabla = "LND_CreditApplicationDocuments";

    public void Configure(EntityTypeBuilder<CreditApplicationDocument> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.AttachmentPublicId).IsRequired();
        builder.Property(e => e.DocumentDate);
        builder.Property(e => e.VerifiedAt);
        builder.Property(e => e.RejectionReason).HasMaxLength(300);

        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLineDocumentRequirement>().WithMany().HasForeignKey(e => e.RequirementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditApplicationParty>().WithMany().HasForeignKey(e => e.PartyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.VerifiedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
