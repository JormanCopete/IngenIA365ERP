using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// <c>ADM_NationalValues</c> (feature 013, T37, T38; data-model parte 3 §3.1): <c>Sequence</c> única, una fila se reemplaza a
/// lo sumo una vez (<c>UK_ADM_NationalValues_Supersedes</c> filtrado no nulo) y la búsqueda por código, clave y vigencia.
/// Migración Admin <c>ValoresNacionales</c>. (nuevo)
/// </summary>
public class PlatformNationalValueConfiguration : IEntityTypeConfiguration<PlatformNationalValue>
{
    public void Configure(EntityTypeBuilder<PlatformNationalValue> builder)
    {
        builder.ToTable("ADM_NationalValues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ADM_NationalValues_PublicId");

        builder.Property(e => e.Code).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Key).HasMaxLength(60).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.ReferenceDate);
        builder.Property(e => e.Value).HasPrecision(20, 10);
        builder.Property(e => e.Unit).HasMaxLength(12);
        builder.Property(e => e.Label).HasMaxLength(200);
        builder.Property(e => e.Source).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.Sequence).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.RegisteredBy).HasMaxLength(150).IsRequired();
        builder.Property(e => e.RegisteredByCentralUserId).IsRequired();
        builder.Property(e => e.SupersedesPublicId);

        builder.HasIndex(e => e.Sequence).IsUnique().HasDatabaseName("UK_ADM_NationalValues_Sequence");
        builder.HasIndex(e => e.SupersedesPublicId).IsUnique()
            .HasFilter("[SupersedesPublicId] IS NOT NULL")
            .HasDatabaseName("UK_ADM_NationalValues_Supersedes");
        builder.HasIndex(e => new { e.Code, e.Key, e.ValidFrom }).HasDatabaseName("IX_ADM_NationalValues_Code_Key_ValidFrom");

        ConfiguracionDeHechoCentral.Comunes(builder);
    }
}

/// <summary>
/// <c>ADM_RegulatoryDocuments</c> (feature 013, T37, T45; data-model parte 3 §3.2): el contenido es texto (no <c>jsonb</c>, por
/// paridad) y la huella <c>char(64)</c>. (nuevo)
/// </summary>
public class PlatformRegulatoryDocumentConfiguration : IEntityTypeConfiguration<PlatformRegulatoryDocument>
{
    public void Configure(EntityTypeBuilder<PlatformRegulatoryDocument> builder)
    {
        builder.ToTable("ADM_RegulatoryDocuments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ADM_RegulatoryDocuments_PublicId");

        builder.Property(e => e.Kind).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(60).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.ContentSha256).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(e => e.SchemaVersion).IsRequired();
        builder.Property(e => e.Verified).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.Sequence).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.RegisteredBy).HasMaxLength(150).IsRequired();
        builder.Property(e => e.RegisteredByCentralUserId).IsRequired();
        builder.Property(e => e.SupersedesPublicId);

        builder.HasIndex(e => e.Sequence).IsUnique().HasDatabaseName("UK_ADM_RegulatoryDocuments_Sequence");
        builder.HasIndex(e => e.SupersedesPublicId).IsUnique()
            .HasFilter("[SupersedesPublicId] IS NOT NULL")
            .HasDatabaseName("UK_ADM_RegulatoryDocuments_Supersedes");
        builder.HasIndex(e => new { e.Kind, e.ValidFrom }).HasDatabaseName("IX_ADM_RegulatoryDocuments_Kind_ValidFrom");

        ConfiguracionDeHechoCentral.Comunes(builder);
    }
}

/// <summary><c>ADM_NationalValueCounters</c> (feature 013, C44; data-model parte 3 §3.3): fila única con <c>NextValue</c> y <c>RowVersion</c>. (nuevo)</summary>
public class PlatformNationalValueCounterConfiguration : IEntityTypeConfiguration<PlatformNationalValueCounter>
{
    public void Configure(EntityTypeBuilder<PlatformNationalValueCounter> builder)
    {
        builder.ToTable("ADM_NationalValueCounters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ADM_NationalValueCounters_PublicId");

        builder.Property(e => e.NextValue).IsRequired();
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.Property(e => e.CreatedBy).HasMaxLength(150);
        builder.Property(e => e.UpdatedBy).HasMaxLength(150);
        builder.Property(e => e.DeletedBy).HasMaxLength(150);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Lo común de los dos hechos de la central: auditoría, <c>RowVersion</c> y borrado lógico (que nunca se usa).</summary>
internal static class ConfiguracionDeHechoCentral
{
    public static void Comunes<T>(EntityTypeBuilder<T> builder) where T : Domain.Common.AuditableEntity
    {
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.Property(e => e.CreatedBy).HasMaxLength(150);
        builder.Property(e => e.UpdatedBy).HasMaxLength(150);
        builder.Property(e => e.DeletedBy).HasMaxLength(150);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
