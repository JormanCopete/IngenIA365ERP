using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Enums.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Compliance;

public class HabeasDataConsentConfiguration : IEntityTypeConfiguration<HabeasDataConsent>
{
    public void Configure(EntityTypeBuilder<HabeasDataConsent> builder)
    {
        builder.ToTable("CMP_HabeasDataConsents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique()
            .HasDatabaseName("UK_CMP_HabeasConsents_PublicId");

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.PersonId).IsRequired();
        builder.Property(e => e.PolicyVersionId).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ActionAt).IsRequired();
        builder.Property(e => e.ActionBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Channel).HasMaxLength(50);
        builder.Property(e => e.Notes).HasMaxLength(2000);

        // Feature 013 (T69 a, T0086; data-model parte 3 §4.10): columnas aditivas, como texto (igual que Action). Las filas
        // existentes toman los DEFAULT; la migración las trae con PlataformaParaCartera.
        builder.Property(e => e.Purpose).HasConversion<string>().HasMaxLength(30).IsRequired()
            .HasDefaultValue(DataAuthorizationPurpose.GeneralTreatment).HasSentinel((DataAuthorizationPurpose)0);
        builder.Property(e => e.ContactChannel).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.EvidenceAttachmentPublicId);
        builder.Property(e => e.Origin).HasConversion<string>().HasMaxLength(30).IsRequired()
            .HasDefaultValue(DataAuthorizationOrigin.PersonRegistration).HasSentinel((DataAuthorizationOrigin)0);

        // Lookup patrón "historial del titular": (TenantId, PersonId, ActionAt).
        builder.HasIndex(e => new { e.TenantId, e.PersonId, e.ActionAt })
            .HasDatabaseName("IX_CMP_HabeasConsents_History")
            .HasFilter("[IsDeleted] = 0");

        // La autorización vigente por finalidad y canal (IAutorizacionDeDatos.VigenteAsync(persona, finalidad, canal?)).
        builder.HasIndex(e => new { e.PersonId, e.Purpose, e.ContactChannel, e.ActionAt })
            .HasDatabaseName("IX_CMP_HabeasConsents_Purpose")
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(e => e.PolicyVersion).WithMany()
            .HasForeignKey(e => e.PolicyVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
