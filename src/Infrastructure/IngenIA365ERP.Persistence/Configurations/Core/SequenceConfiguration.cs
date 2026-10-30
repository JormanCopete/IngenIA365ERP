using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class SequenceConfiguration : IEntityTypeConfiguration<Sequence>
{
    public void Configure(EntityTypeBuilder<Sequence> builder)
    {
        builder.ToTable("COR_Sequences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Sequences_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(10);
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(30);
        builder.Property(e => e.DocumentType).HasMaxLength(10);
        builder.Property(e => e.IsAutomatic).HasDefaultValue(false);
        builder.Property(e => e.NextSequence).HasDefaultValue(1L);

        // Feature 015 (T0039, R-13; decisión del dueño del 2026-10-24, DT §2.7 «(nuevo)»): Recaudo y Tesorería numeran sobre COR_Sequences con la
        // clave en Name (RCD:{code}:{prefijo}, TES:{code}) y el módulo en DocumentType. El índice único filtrado a sus filas es el que deja al
        // cerrojo crear la fila que falta sin que dos operaciones numeren lo mismo (INSERT … ON CONFLICT / WHERE NOT EXISTS); las filas
        // heredadas de sys_consecu quedan fuera del filtro.
        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("UK_COR_Sequences_Name_RcdTes").HasFilter("[DocumentType] IN ('RCD', 'TES')");

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
