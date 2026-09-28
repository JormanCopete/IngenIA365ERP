using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.ElectronicInvoicing;

/// <summary>
/// <c>COR_DianNumberingResolutions</c> (feature 012, I4, T698; data-model §18): único
/// <c>(Environment, Prefix, ResolutionNumber)</c> entre vivos e índice <c>(Kind, Environment, Prefix, ValidFrom)</c> para
/// elegir la vigente. <c>LastIssuedNumber</c> (bigint) lo escribe sólo <c>NumeradorFiscal</c> y <c>RowVersion</c> es su
/// control de concurrencia. Llega con la migración <c>DocumentosElectronicos</c>.
/// </summary>
public class DianNumberingResolutionConfiguration : IEntityTypeConfiguration<DianNumberingResolution>
{
    public const string Tabla = "COR_DianNumberingResolutions";

    public void Configure(EntityTypeBuilder<DianNumberingResolution> builder)
    {
        builder.ComoEntidadDeFacturacionElectronica(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.BacksUpKind).HasConversion<int?>();
        builder.Property(e => e.ResolutionNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ResolutionDate).IsRequired();
        builder.Property(e => e.Prefix).HasMaxLength(4).IsRequired();
        builder.Property(e => e.RangeFrom).IsRequired();
        builder.Property(e => e.RangeTo).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo).IsRequired();
        builder.Property(e => e.Environment).HasConversion<int>().IsRequired();
        builder.Property(e => e.LastIssuedNumber).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(300);

        builder.Ignore(e => e.TieneNumerosEmitidos);
        builder.Ignore(e => e.Agotada);
        builder.Ignore(e => e.Disponibles);

        builder.HasMany(e => e.Channels).WithOne(c => c.Resolution).HasForeignKey(c => c.ResolutionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Environment, e.Prefix, e.ResolutionNumber })
            .IsUnique()
            .HasDatabaseName("UK_COR_DianNumberingResolutions_Environment_Prefix_Number")
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.Kind, e.Environment, e.Prefix, e.ValidFrom })
            .HasDatabaseName("IX_COR_DianNumberingResolutions_Kind_Environment_Prefix_ValidFrom");
    }
}
