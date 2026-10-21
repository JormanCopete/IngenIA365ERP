using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

/// <summary>
/// <c>COR_NationalValues</c> (feature 013, T37, T39; data-model parte 3 §4.1): la copia por cooperativa del registro central,
/// única por <c>SourcePublicId</c> y por <c>SourceSequence</c> (guardas de idempotencia de la réplica), con la réplica que la
/// trajo (FK <c>Restrict</c>). Migración <c>PlataformaParaCartera</c>. (nuevo)
/// </summary>
public class NationalValueConfiguration : IEntityTypeConfiguration<NationalValue>
{
    public void Configure(EntityTypeBuilder<NationalValue> builder)
    {
        builder.ToTable("COR_NationalValues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_NationalValues_PublicId");

        builder.Property(e => e.Code).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Key).HasMaxLength(60).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Value).HasPrecision(20, 10);
        builder.Property(e => e.Unit).HasMaxLength(12);
        builder.Property(e => e.Label).HasMaxLength(200);
        builder.Property(e => e.Source).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.SourcePublicId).IsRequired();
        builder.Property(e => e.SourceSequence).IsRequired();
        builder.Property(e => e.CentralRegisteredAt).IsRequired();
        builder.Property(e => e.KnownAt).IsRequired();

        builder.HasOne(e => e.Replication).WithMany(r => r.Values).HasForeignKey(e => e.ReplicationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.SourcePublicId).IsUnique().HasDatabaseName("UK_COR_NationalValues_SourcePublicId");
        builder.HasIndex(e => e.SourceSequence).IsUnique().HasDatabaseName("UK_COR_NationalValues_SourceSequence");
        builder.HasIndex(e => new { e.Code, e.Key, e.ValidFrom }).HasDatabaseName("IX_COR_NationalValues_Code_Key_ValidFrom");

        ConfiguracionDeCopiaNacional.Comunes(builder);
    }
}

/// <summary><c>COR_RegulatoryDocuments</c> (feature 013, T37, T39; data-model parte 3 §4.2). (nuevo)</summary>
public class RegulatoryDocumentConfiguration : IEntityTypeConfiguration<RegulatoryDocument>
{
    public void Configure(EntityTypeBuilder<RegulatoryDocument> builder)
    {
        builder.ToTable("COR_RegulatoryDocuments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_RegulatoryDocuments_PublicId");

        builder.Property(e => e.Kind).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(60).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.ContentSha256).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(e => e.SchemaVersion).IsRequired();
        builder.Property(e => e.Verified).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.SourcePublicId).IsRequired();
        builder.Property(e => e.SourceSequence).IsRequired();
        builder.Property(e => e.CentralRegisteredAt).IsRequired();
        builder.Property(e => e.KnownAt).IsRequired();

        builder.HasOne(e => e.Replication).WithMany(r => r.Documents).HasForeignKey(e => e.ReplicationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.SourcePublicId).IsUnique().HasDatabaseName("UK_COR_RegulatoryDocuments_SourcePublicId");
        builder.HasIndex(e => e.SourceSequence).IsUnique().HasDatabaseName("UK_COR_RegulatoryDocuments_SourceSequence");
        builder.HasIndex(e => new { e.Kind, e.ValidFrom }).HasDatabaseName("IX_COR_RegulatoryDocuments_Kind_ValidFrom");

        ConfiguracionDeCopiaNacional.Comunes(builder);
    }
}

/// <summary><c>COR_NationalValueReplications</c> (feature 013, T39; data-model parte 3 §4.3): una fila por réplica que copió algo. (nuevo)</summary>
public class NationalValueReplicationConfiguration : IEntityTypeConfiguration<NationalValueReplication>
{
    public void Configure(EntityTypeBuilder<NationalValueReplication> builder)
    {
        builder.ToTable("COR_NationalValueReplications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_NationalValueReplications_PublicId");

        builder.Property(e => e.FromSequence).IsRequired();
        builder.Property(e => e.ToSequence).IsRequired();
        builder.Property(e => e.ValueCount).IsRequired();
        builder.Property(e => e.DocumentCount).IsRequired();
        builder.Property(e => e.Origin).HasConversion<int>().IsRequired();
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.FinishedAt).IsRequired();
        builder.Property(e => e.EffectsSummaryJson).HasMaxLength(2000);
        builder.Property(e => e.ActorName).HasMaxLength(150).IsRequired();

        builder.HasIndex(e => e.StartedAt).HasDatabaseName("IX_COR_NationalValueReplications_StartedAt");

        ConfiguracionDeCopiaNacional.Comunes(builder);
    }
}

/// <summary><c>COR_NationalValueReviews</c> (feature 013, T41, R7; data-model parte 3 §4.4): sin FK a la copia (apunta a una de dos tablas). (nuevo)</summary>
public class NationalValueReviewConfiguration : IEntityTypeConfiguration<NationalValueReview>
{
    public void Configure(EntityTypeBuilder<NationalValueReview> builder)
    {
        builder.ToTable("COR_NationalValueReviews");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_NationalValueReviews_PublicId");

        builder.Property(e => e.TargetKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.TargetPublicId).IsRequired();
        builder.Property(e => e.Decision).HasConversion<int>().IsRequired();
        builder.Property(e => e.Note).HasMaxLength(1000);
        builder.Property(e => e.ReviewedByUserId).IsRequired();
        builder.Property(e => e.ReviewedByName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ReviewedAt).IsRequired();

        builder.HasOne<Domain.Entities.Security.User>().WithMany()
            .HasForeignKey(e => e.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TargetPublicId, e.ReviewedAt }).HasDatabaseName("IX_COR_NationalValueReviews_Target");

        ConfiguracionDeCopiaNacional.Comunes(builder);
    }
}

/// <summary>Lo común de las tablas de valores nacionales de la cooperativa. (nuevo)</summary>
internal static class ConfiguracionDeCopiaNacional
{
    public static void Comunes<T>(EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.Property(e => e.CreatedBy).HasMaxLength(150);
        builder.Property(e => e.UpdatedBy).HasMaxLength(150);
        builder.Property(e => e.DeletedBy).HasMaxLength(150);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
