using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Integration;

/// <summary>
/// <c>COR_IntegrationMessageDependencies</c> (feature 012, T9, T074; data-model §19). Dos FK <c>Restrict</c> a
/// <c>COR_IntegrationMessages</c>: nada se borra en cascada. Único <c>(MessageId, DependsOnMessageId)</c> y un
/// índice por <c>DependsOnMessageId</c> para encontrar a quién bloquea un mensaje. La tabla llega con la
/// migración <c>PlataformaParaInventario</c>.
/// </summary>
public class IntegrationMessageDependencyConfiguration : IEntityTypeConfiguration<IntegrationMessageDependency>
{
    public void Configure(EntityTypeBuilder<IntegrationMessageDependency> builder)
    {
        builder.ToTable("COR_IntegrationMessageDependencies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_IntegrationMessageDependencies_PublicId");

        builder.HasOne(e => e.Message).WithMany().HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DependsOnMessage).WithMany().HasForeignKey(e => e.DependsOnMessageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MessageId, e.DependsOnMessageId }).IsUnique()
            .HasDatabaseName("UK_COR_IntegrationMessageDependencies_Message_DependsOn");
        builder.HasIndex(e => e.DependsOnMessageId).HasDatabaseName("IX_COR_IntegrationMessageDependencies_DependsOn");

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
