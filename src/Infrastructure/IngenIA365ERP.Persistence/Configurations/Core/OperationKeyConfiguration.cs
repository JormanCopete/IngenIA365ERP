using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

/// <summary>
/// <c>COR_OperationKeys</c> (feature 012, T054; data-model §19). El único de <c>Key</c> va <b>sin filtro</b>:
/// una clave usada no se vuelve a usar nunca, ni aunque alguien marcara la fila como borrada, y es el índice
/// en el que espera el duplicado concurrente. <c>IdempotencyBehavior</c> reconoce la colisión por el nombre
/// del índice (<see cref="OperationKey.IndiceUnicoDeLaClave"/>). La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class OperationKeyConfiguration : IEntityTypeConfiguration<OperationKey>
{
    public void Configure(EntityTypeBuilder<OperationKey> builder)
    {
        builder.ToTable("COR_OperationKeys");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_OperationKeys_PublicId");

        builder.Property(e => e.Key).IsRequired();
        builder.HasIndex(e => e.Key).IsUnique().HasDatabaseName(OperationKey.IndiceUnicoDeLaClave);

        builder.Property(e => e.Operation).HasMaxLength(120).IsRequired();
        builder.Property(e => e.RequestSha256).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(e => e.CentralUserId).IsRequired();
        builder.Property(e => e.ActorName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ResultJson);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
