using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

// Feature 012, entrega I2 (T481; data-model §20; contracts/contabilidad.md §2 y §3.1): el lado contable de la integración
// con Inventario, enmienda de la 009. Las tres tablas llegan con la migración IntegracionContableDeInventario (T486).
// Ninguna tiene llave a tablas INV_: las dimensiones de Inventario van por código (T27) y los documentos de origen por
// su PublicId, así Contabilidad no acopla su esquema al de Inventario.

/// <summary>
/// <c>ACC_InventoryPostingRules</c>: <c>UK (DimensionKey, ValidFrom)</c> filtrado a vivas —la clave lleva <c>*</c> donde la
/// regla no fija valor, así el único no depende de cómo cada motor trata los nulos—, índice <c>(Operation, Role, ValidFrom)</c>
/// para la resolución y llaves <c>Restrict</c> a la cuenta, la sucursal y el centro de costo (lo único de Core con llave).
/// </summary>
public class InventoryPostingRuleConfiguration : IEntityTypeConfiguration<InventoryPostingRule>
{
    public void Configure(EntityTypeBuilder<InventoryPostingRule> builder)
    {
        builder.ToTable("ACC_InventoryPostingRules");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_InventoryPostingRules_PublicId");

        builder.Property(e => e.Operation).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Role).HasMaxLength(30).IsRequired();
        builder.Property(e => e.AccountingGroupCode).HasMaxLength(10);
        builder.Property(e => e.WarehouseCode).HasMaxLength(10);
        builder.Property(e => e.PointOfSaleCode).HasMaxLength(10);
        builder.Property(e => e.PaymentMeansCode).HasMaxLength(10);
        builder.Property(e => e.TaxRateCode).HasMaxLength(10);
        builder.Property(e => e.TaxRate).HasPrecision(9, 6);
        builder.Property(e => e.ReasonCode).HasMaxLength(40);
        builder.Property(e => e.DimensionKey).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SpecificityWeight).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(300).IsRequired();

        builder.HasOne<ChartOfAccount>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DimensionKey, e.ValidFrom }).IsUnique()
            .HasDatabaseName("UK_ACC_InventoryPostingRules_DimensionKey_ValidFrom").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.Operation, e.Role, e.ValidFrom }).HasDatabaseName("IX_ACC_InventoryPostingRules_Operation_Role_ValidFrom");
        builder.HasIndex(e => e.AccountId).HasDatabaseName("IX_ACC_InventoryPostingRules_Account");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("IX_ACC_InventoryPostingRules_Branch");
        builder.HasIndex(e => e.CostCenterId).HasDatabaseName("IX_ACC_InventoryPostingRules_CostCenter");
    }
}

/// <summary>
/// <c>ACC_InventoryVoucherMappings</c>: <c>MappingKey</c> (<c>{Operation}|{tipo de documento|*}</c>) único entre vivas;
/// llaves <c>Restrict</c> al tipo de comprobante y al documento cruce.
/// </summary>
public class InventoryVoucherMappingConfiguration : IEntityTypeConfiguration<InventoryVoucherMapping>
{
    public void Configure(EntityTypeBuilder<InventoryVoucherMapping> builder)
    {
        builder.ToTable("ACC_InventoryVoucherMappings");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_InventoryVoucherMappings_PublicId");

        builder.Property(e => e.Operation).HasMaxLength(30).IsRequired();
        builder.Property(e => e.InventoryDocumentTypeCode).HasMaxLength(10);
        builder.Property(e => e.MappingKey).HasMaxLength(50).IsRequired();

        builder.HasOne(e => e.VoucherType).WithMany().HasForeignKey(e => e.VoucherTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CrossDocumentType).WithMany().HasForeignKey(e => e.CrossDocumentTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MappingKey).IsUnique()
            .HasDatabaseName("UK_ACC_InventoryVoucherMappings_MappingKey").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.VoucherTypeId).HasDatabaseName("IX_ACC_InventoryVoucherMappings_VoucherType");
        builder.HasIndex(e => e.CrossDocumentTypeId).HasDatabaseName("IX_ACC_InventoryVoucherMappings_CrossDocumentType");
    }
}

/// <summary>
/// <c>ACC_InventoryPostings</c>: el recibo por mensaje. <c>UK_ACC_InventoryPostings_MessagePublicId</c> va <b>sin filtro</b>
/// —un mensaje procesado no se vuelve a procesar nunca— y es el índice en el que choca un segundo consumo concurrente
/// (<see cref="IndiceUnicoDelMensaje"/>, que el consumidor traduce a <c>AlreadyProcessed</c>). Índices de lectura por
/// documento de origen, relacionado, comprobante, lote y fecha; crece millones de filas al año, por eso <c>bigint</c>.
/// </summary>
public class InventoryPostingConfiguration : IEntityTypeConfiguration<InventoryPosting>
{
    public const string IndiceUnicoDelMensaje = "UK_ACC_InventoryPostings_MessagePublicId";

    public void Configure(EntityTypeBuilder<InventoryPosting> builder)
    {
        builder.ToTable("ACC_InventoryPostings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_InventoryPostings_PublicId");

        builder.Property(e => e.MessagePublicId).IsRequired();
        builder.Property(e => e.MessageType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.MessageVersion).IsRequired();
        builder.Property(e => e.MessageKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.SourceModule).HasMaxLength(3).IsRequired();
        builder.Property(e => e.SourcePublicId).IsRequired();
        builder.Property(e => e.SourceDocumentClass).HasMaxLength(40);
        builder.Property(e => e.SourceDocumentTypeCode).HasMaxLength(10);
        builder.Property(e => e.SourceDocumentNumber).HasMaxLength(30);
        builder.Property(e => e.OperationDate).IsRequired();
        builder.Property(e => e.NoVoucherReason).HasMaxLength(20);
        builder.Property(e => e.OriginUserCentralId).IsRequired();
        builder.Property(e => e.OriginUserName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ActorKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.ActorName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ProcessedAt).IsRequired();

        builder.HasOne(e => e.AccountingDocument).WithMany().HasForeignKey(e => e.AccountingDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MessagePublicId).IsUnique().HasDatabaseName(IndiceUnicoDelMensaje);
        builder.HasIndex(e => e.SourcePublicId).HasDatabaseName("IX_ACC_InventoryPostings_Source");
        builder.HasIndex(e => e.RelatedDocumentPublicId).HasDatabaseName("IX_ACC_InventoryPostings_Related");
        builder.HasIndex(e => e.AccountingDocumentId).HasDatabaseName("IX_ACC_InventoryPostings_AccountingDocument");
        builder.HasIndex(e => e.BatchPublicId).HasDatabaseName("IX_ACC_InventoryPostings_Batch");
        builder.HasIndex(e => e.OperationDate).HasDatabaseName("IX_ACC_InventoryPostings_OperationDate");
        builder.HasIndex(e => e.ActorUserId).HasDatabaseName("IX_ACC_InventoryPostings_ActorUser");
    }
}
