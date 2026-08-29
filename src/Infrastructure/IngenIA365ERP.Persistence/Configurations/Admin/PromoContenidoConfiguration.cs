using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

public class PromoContenidoConfiguration : IEntityTypeConfiguration<PromoContenido>
{
    public void Configure(EntityTypeBuilder<PromoContenido> builder)
    {
        builder.ToTable("ADM_PromoContenidos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Titulo).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Texto).HasMaxLength(400);
        builder.Property(e => e.TextoEnlace).HasMaxLength(60);
        builder.Property(e => e.Enlace).HasMaxLength(500);
        builder.Property(e => e.ImagenTipoMime).HasMaxLength(60);
        builder.Property(e => e.ImagenTextoAlternativo).HasMaxLength(200);

        // La consulta del login pide las publicadas ordenadas. Es la única que
        // corre sin autenticar y en cada carga de la pantalla de entrada.
        builder.HasIndex(e => new { e.Publicado, e.Orden })
            .HasDatabaseName("IX_ADM_PromoContenidos_Publicado_Orden");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
