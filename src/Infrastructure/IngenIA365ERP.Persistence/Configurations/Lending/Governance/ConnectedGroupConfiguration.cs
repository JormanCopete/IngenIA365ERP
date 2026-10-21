using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Governance;

/// <summary>
/// <c>LND_ConnectedGroups</c> (feature 013, T0213; DT §7.10 C112): hecho con fundamento obligatorio; la terminación
/// (<c>EndedAt</c>, <c>EndReason</c>) es de escritura única.
/// </summary>
public class ConnectedGroupConfiguration : IEntityTypeConfiguration<ConnectedGroup>
{
    public const string Tabla = "LND_ConnectedGroups";

    public void Configure(EntityTypeBuilder<ConnectedGroup> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Basis).HasConversion<int>().IsRequired();
        builder.Property(e => e.Grounds).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.SupportAttachmentPublicId);
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.EndedAt);
        builder.Property(e => e.EndReason).HasMaxLength(500);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
