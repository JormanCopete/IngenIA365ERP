using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Integration;

/// <summary>
/// <c>COR_IntegrationMessages</c> (feature 012, T7, T074; data-model §19). El <c>Id</c> bigint identidad es el
/// orden de entrega. <c>PayloadJson</c> es texto (<c>nvarchar(max)</c> / <c>text</c>) y nunca <c>jsonb</c>: los
/// dos motores guardan los mismos bytes y el SHA-256 se calcula sobre ellos. El único
/// <c>(OriginPublicId, Type, OriginEventKey)</c> es la segunda capa de idempotencia (contracts/mensajes.md
/// §10.2): una doble emisión hace fallar el <c>SaveChanges</c> entero. La tabla llega con la migración
/// <c>PlataformaParaInventario</c>.
/// </summary>
public class IntegrationMessageConfiguration : IEntityTypeConfiguration<IntegrationMessage>
{
    public void Configure(EntityTypeBuilder<IntegrationMessage> builder)
    {
        builder.ToTable("COR_IntegrationMessages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_IntegrationMessages_PublicId");

        builder.Property(e => e.Type).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.OriginModule).HasMaxLength(3).IsRequired();
        builder.Property(e => e.OriginKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.OriginPublicId).IsRequired();
        builder.Property(e => e.OriginDocumentClass).HasMaxLength(40);
        builder.Property(e => e.OriginDocumentTypeCode).HasMaxLength(10);
        builder.Property(e => e.OriginNumber).HasMaxLength(30);
        builder.Property(e => e.OriginEventKey).HasMaxLength(80).IsRequired();
        builder.Property(e => e.FiscalUniqueCode).HasMaxLength(96);
        builder.Property(e => e.RelatedPublicId);
        builder.Property(e => e.RelatedDocumentClass).HasMaxLength(40);
        builder.Property(e => e.RelatedNumber).HasMaxLength(30);
        builder.Property(e => e.ChainRootPublicId).IsRequired();
        builder.Property(e => e.OperationDate).IsRequired();
        builder.Property(e => e.BranchPublicId).IsRequired();
        builder.Property(e => e.CostCenterPublicId);
        builder.Property(e => e.WarehouseCode).HasMaxLength(10);
        builder.Property(e => e.PersonPublicId);
        builder.Property(e => e.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(e => e.ExchangeRate).HasPrecision(18, 6).IsRequired();
        builder.Property(e => e.PayloadJson).IsRequired();
        builder.Property(e => e.PayloadSha256).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(e => e.PrevalidationOutcome).HasConversion<int?>();
        builder.Property(e => e.OriginUserCentralId).IsRequired();
        builder.Property(e => e.OriginUserName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.EmittedAt).IsRequired();

        builder.HasIndex(e => new { e.OriginPublicId, e.Type, e.OriginEventKey }).IsUnique()
            .HasDatabaseName("UK_COR_IntegrationMessages_Origin_Type_EventKey");
        builder.HasIndex(e => new { e.ChainRootPublicId, e.Id }).HasDatabaseName("IX_COR_IntegrationMessages_ChainRoot");
        builder.HasIndex(e => e.OperationDate).HasDatabaseName("IX_COR_IntegrationMessages_OperationDate");
        builder.HasIndex(e => new { e.Type, e.OperationDate }).HasDatabaseName("IX_COR_IntegrationMessages_Type_OperationDate");
        builder.HasIndex(e => e.PersonPublicId).HasDatabaseName("IX_COR_IntegrationMessages_Person")
            .HasFilter("[PersonPublicId] IS NOT NULL");

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
