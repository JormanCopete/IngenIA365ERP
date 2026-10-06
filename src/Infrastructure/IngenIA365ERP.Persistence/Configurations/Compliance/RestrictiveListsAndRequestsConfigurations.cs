using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Compliance;

/// <summary>
/// <c>COR_RestrictiveListLoads</c> (feature 013, T70, T0087; data-model parte 3 §4.8): una versión por lista (<c>UK_…_Code_Version</c>
/// filtrado vivos), quién cargó contra <c>SEC_Users</c>. Migración <c>PlataformaParaCartera</c>. (nuevo)
/// </summary>
public class RestrictiveListLoadConfiguration : IEntityTypeConfiguration<RestrictiveListLoad>
{
    public void Configure(EntityTypeBuilder<RestrictiveListLoad> builder)
    {
        builder.ToTable("COR_RestrictiveListLoads");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_RestrictiveListLoads_PublicId");

        builder.Property(e => e.ListCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(300).IsRequired();
        builder.Property(e => e.PublishedOn);
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(260).IsRequired();
        builder.Property(e => e.FileSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(e => e.EntryCount).IsRequired();
        builder.Property(e => e.LoadedByUserId).IsRequired();
        builder.Property(e => e.LoadedByName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(300);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.LoadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Entries).WithOne(e => e.Load).HasForeignKey(e => e.LoadId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ListCode, e.Version }).IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UK_COR_RestrictiveListLoads_Code_Version");

        ComunesDeCumplimiento.Aplicar(builder);
    }
}

/// <summary>
/// <c>COR_RestrictiveListEntries</c> (feature 013, T70, T0087; data-model parte 3 §4.8): búsqueda por documento normalizado (filtrado no
/// nulo) y por carga. (nuevo)
/// </summary>
public class RestrictiveListEntryConfiguration : IEntityTypeConfiguration<RestrictiveListEntry>
{
    public void Configure(EntityTypeBuilder<RestrictiveListEntry> builder)
    {
        builder.ToTable("COR_RestrictiveListEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_RestrictiveListEntries_PublicId");

        builder.Property(e => e.LoadId).IsRequired();
        builder.Property(e => e.EntityKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.DocumentType).HasMaxLength(10);
        builder.Property(e => e.DocumentNumberNormalized).HasMaxLength(30);
        builder.Property(e => e.FullName).HasMaxLength(300).IsRequired();
        builder.Property(e => e.NameNormalized).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Aliases).HasMaxLength(2000);
        builder.Property(e => e.Program).HasMaxLength(100);
        builder.Property(e => e.Notes).HasMaxLength(1000);

        builder.HasIndex(e => e.DocumentNumberNormalized)
            .HasFilter("[DocumentNumberNormalized] IS NOT NULL")
            .HasDatabaseName("IX_COR_RestrictiveListEntries_Document");
        builder.HasIndex(e => e.LoadId).HasDatabaseName("IX_COR_RestrictiveListEntries_Load");

        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.Property(e => e.CreatedBy).HasMaxLength(150);
        builder.Property(e => e.UpdatedBy).HasMaxLength(150);
        builder.Property(e => e.DeletedBy).HasMaxLength(150);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>
/// <c>COR_DataSubjectRequests</c> (feature 013, T69 b, T0088; data-model parte 3 §4.9): la bandeja por estado y vencimiento y el historial
/// por titular. Finalidad y canal como texto (igual que en <c>CMP_HabeasDataConsents</c>); clase, estado y desenlace como int. (nuevo)
/// </summary>
public class DataSubjectRequestConfiguration : IEntityTypeConfiguration<DataSubjectRequest>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequest> builder)
    {
        builder.ToTable("COR_DataSubjectRequests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_DataSubjectRequests_PublicId");

        builder.Property(e => e.PersonId).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Purpose).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.ContactChannel).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ReceivedVia).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ReceivedAt).IsRequired();
        builder.Property(e => e.ReceivedOn).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.EvidenceAttachmentPublicId);
        builder.Property(e => e.DueDate).IsRequired();
        builder.Property(e => e.ExtendedDueDate);
        builder.Property(e => e.ExtensionReason).HasMaxLength(500);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int?>();
        builder.Property(e => e.DutyBasis).HasMaxLength(1000);
        builder.Property(e => e.DutyEvidenceJson);
        builder.Property(e => e.ResponseText).HasMaxLength(4000);
        builder.Property(e => e.ResponseAttachmentPublicId);
        builder.Property(e => e.RespondedAt);
        builder.Property(e => e.RespondedByName).HasMaxLength(150);
        builder.Property(e => e.RegisteredByUserId).IsRequired();
        builder.Property(e => e.RegisteredByName).HasMaxLength(150).IsRequired();
        builder.Ignore(e => e.VenceEl);
        builder.Ignore(e => e.Abierta);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RespondedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Status, e.DueDate }).HasDatabaseName("IX_COR_DataSubjectRequests_Status_Due");
        builder.HasIndex(e => new { e.PersonId, e.ReceivedAt }).HasDatabaseName("IX_COR_DataSubjectRequests_Person");

        ComunesDeCumplimiento.Aplicar(builder);
    }
}

/// <summary>Las columnas de auditoría y el filtro de borrado lógico de las tablas de cumplimiento de la 013. (nuevo)</summary>
internal static class ComunesDeCumplimiento
{
    public static void Aplicar<T>(EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.Property(e => e.CreatedBy).HasMaxLength(150);
        builder.Property(e => e.UpdatedBy).HasMaxLength(150);
        builder.Property(e => e.DeletedBy).HasMaxLength(150);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
