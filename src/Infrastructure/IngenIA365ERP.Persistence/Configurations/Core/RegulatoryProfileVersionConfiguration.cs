using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

/// <summary>
/// <c>COR_RegulatoryProfileVersions</c> (feature 013, T50, R15; data-model parte 3 §4.5): el perfil regulatorio con vigencia. Hecho de
/// sólo inserción; una corrección por versión (<c>UK_COR_RegulatoryProfileVersions_Supersedes</c>, filtrado no nulo). Migración
/// <c>PlataformaParaCartera</c>. (nuevo)
/// </summary>
public class RegulatoryProfileVersionConfiguration : IEntityTypeConfiguration<RegulatoryProfileVersion>
{
    public void Configure(EntityTypeBuilder<RegulatoryProfileVersion> builder)
    {
        builder.ToTable("COR_RegulatoryProfileVersions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_RegulatoryProfileVersions_PublicId");

        builder.Property(e => e.OrganizationTypeCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.FinancialActivityAuthorized).IsRequired();
        builder.Property(e => e.SupervisionLevel).IsRequired();
        builder.Property(e => e.EmployeeFundCategory).HasMaxLength(20);
        builder.Property(e => e.NiifGroup);
        builder.Property(e => e.LibranzaOperatorRegistered).IsRequired();
        builder.Property(e => e.LibranzaOperatorRegistrationNumber).HasMaxLength(40);
        builder.Property(e => e.LibranzaOperatorRegistrationDate);
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(300).IsRequired();
        builder.Property(e => e.SupportAttachmentPublicId);
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.SupersedesPublicId);
        builder.Property(e => e.RegisteredByUserId).IsRequired();
        builder.Property(e => e.RegisteredByName).HasMaxLength(150).IsRequired();

        builder.HasOne<Domain.Entities.Security.User>().WithMany()
            .HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ValidFrom).HasDatabaseName("IX_COR_RegulatoryProfileVersions_ValidFrom");
        builder.HasIndex(e => e.SupersedesPublicId).IsUnique()
            .HasFilter("[SupersedesPublicId] IS NOT NULL")
            .HasDatabaseName("UK_COR_RegulatoryProfileVersions_Supersedes");

        ConfiguracionDeCopiaNacional.Comunes(builder);
    }
}
