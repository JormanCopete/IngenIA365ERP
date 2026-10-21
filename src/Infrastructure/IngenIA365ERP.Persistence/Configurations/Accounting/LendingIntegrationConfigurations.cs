using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

// Feature 013, US7 (T0636; data-model parte 3 §5; contracts/contabilidad.md §3 y §4): el lado contable de la integración con
// Cartera, enmienda E-01 de la 009. Molde: InventoryIntegrationConfigurations (012). Las cuatro tablas llegan con la migración par
// IntegracionContableDeCartera (T0639). Ninguna tiene llave a tablas LND_ (T51): las dimensiones de Cartera van por código, y las
// operaciones, los créditos y los hechos por su PublicId, así Contabilidad no acopla su esquema al de Cartera.

/// <summary>
/// <c>ACC_LendingPostingRules</c>: <c>UK (DimensionKey, ValidFrom)</c> filtrado a vivas —la clave lleva <c>*</c> donde la regla no fija
/// valor, así el único no depende de cómo cada motor trata los nulos—, índice <c>(Operation, Role, ValidFrom)</c> para la resolución y
/// llaves <c>Restrict</c> a la cuenta, la sucursal y el centro de costo.
/// </summary>
public class LendingPostingRuleConfiguration : IEntityTypeConfiguration<LendingPostingRule>
{
    public void Configure(EntityTypeBuilder<LendingPostingRule> builder)
    {
        builder.ToTable("ACC_LendingPostingRules");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_LendingPostingRules_PublicId");

        builder.Property(e => e.Operation).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Role).HasMaxLength(40).IsRequired();
        builder.Property(e => e.CreditLineCode).HasMaxLength(10);
        builder.Property(e => e.ModalityCode).HasMaxLength(30);
        builder.Property(e => e.SubsegmentCode).HasMaxLength(20);
        builder.Property(e => e.RiskCategory).HasMaxLength(1);
        builder.Property(e => e.ConceptCode).HasMaxLength(30);
        builder.Property(e => e.CollectorClass).HasMaxLength(20);
        builder.Property(e => e.ReasonCode).HasMaxLength(40);
        // Feature 014 (T044; contracts/contabilidad.md §1.1): las cinco dimensiones de captaciones, por nombre de su enum, nulas = cualquiera.
        builder.Property(e => e.LineNature).HasMaxLength(20);
        builder.Property(e => e.DepositClass).HasMaxLength(20);
        builder.Property(e => e.IssueTermBand).HasMaxLength(20);
        builder.Property(e => e.ContributionClass).HasMaxLength(20);
        builder.Property(e => e.AccountFlag).HasMaxLength(20);
        builder.Property(e => e.DimensionKey).HasMaxLength(300).IsRequired();
        builder.Property(e => e.SpecificityWeight).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(300).IsRequired();

        builder.HasOne<ChartOfAccount>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DimensionKey, e.ValidFrom }).IsUnique()
            .HasDatabaseName("UK_ACC_LendingPostingRules_DimensionKey_ValidFrom").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.Operation, e.Role, e.ValidFrom }).HasDatabaseName("IX_ACC_LendingPostingRules_Operation_Role");
        builder.HasIndex(e => e.AccountId).HasDatabaseName("IX_ACC_LendingPostingRules_Account");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("IX_ACC_LendingPostingRules_Branch");
        builder.HasIndex(e => e.CostCenterId).HasDatabaseName("IX_ACC_LendingPostingRules_CostCenter");
    }
}

/// <summary>
/// <c>ACC_LendingVoucherMappings</c>: <c>MappingKey</c> (<c>{Operation}|{CollectorClass|*}</c>) único entre vivas; llaves
/// <c>Restrict</c> al tipo de comprobante y al documento cruce.
/// </summary>
public class LendingVoucherMappingConfiguration : IEntityTypeConfiguration<LendingVoucherMapping>
{
    public void Configure(EntityTypeBuilder<LendingVoucherMapping> builder)
    {
        builder.ToTable("ACC_LendingVoucherMappings");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_LendingVoucherMappings_PublicId");

        builder.Property(e => e.Operation).HasMaxLength(40).IsRequired();
        builder.Property(e => e.CollectorClass).HasMaxLength(20);
        builder.Property(e => e.MappingKey).HasMaxLength(70).IsRequired();
        builder.Property(e => e.IsSeeded).IsRequired();
        builder.Property(e => e.ValidatedBy).HasMaxLength(150);

        builder.HasOne(e => e.VoucherType).WithMany().HasForeignKey(e => e.VoucherTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CrossDocumentType).WithMany().HasForeignKey(e => e.CrossDocumentTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MappingKey).IsUnique()
            .HasDatabaseName("UK_ACC_LendingVoucherMappings_MappingKey").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.VoucherTypeId).HasDatabaseName("IX_ACC_LendingVoucherMappings_VoucherType");
        builder.HasIndex(e => e.CrossDocumentTypeId).HasDatabaseName("IX_ACC_LendingVoucherMappings_CrossDocumentType");
    }
}

/// <summary>
/// <c>ACC_LendingPostings</c>: el recibo por hecho. <c>UK_ACC_LendingPostings_MessagePublicId</c> va <b>sin filtro</b> —un hecho
/// procesado no se vuelve a procesar nunca— y es el índice en el que choca un segundo consumo concurrente
/// (<see cref="IndiceUnicoDelMensaje"/>, que el consumidor traduce a <c>AlreadyProcessed</c>). Índices de lectura por origen,
/// relacionado, comprobante, lote y fecha; la causación diaria lo hace crecer millones de filas al año, por eso <c>bigint</c>.
/// </summary>
public class LendingPostingConfiguration : IEntityTypeConfiguration<LendingPosting>
{
    public const string IndiceUnicoDelMensaje = "UK_ACC_LendingPostings_MessagePublicId";

    public void Configure(EntityTypeBuilder<LendingPosting> builder)
    {
        builder.ToTable("ACC_LendingPostings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_LendingPostings_PublicId");

        builder.Property(e => e.MessagePublicId).IsRequired();
        builder.Property(e => e.MessageType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.MessageVersion).IsRequired();
        builder.Property(e => e.MessageKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.SourceModule).HasMaxLength(3).IsRequired();
        builder.Property(e => e.SourcePublicId).IsRequired();
        builder.Property(e => e.SourceDocumentClass).HasMaxLength(40);
        builder.Property(e => e.SourceDocumentTypeCode).HasMaxLength(10);
        builder.Property(e => e.SourceDocumentNumber).HasMaxLength(30);
        builder.Property(e => e.LoanNumber).HasMaxLength(30);
        builder.Property(e => e.OperationDate).IsRequired();
        builder.Property(e => e.NoVoucherReason).HasMaxLength(20);
        builder.Property(e => e.OriginActorKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.OriginUserName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ActorKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.ActorName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ProcessedAt).IsRequired();

        builder.HasOne(e => e.AccountingDocument).WithMany().HasForeignKey(e => e.AccountingDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MessagePublicId).IsUnique().HasDatabaseName(IndiceUnicoDelMensaje);
        builder.HasIndex(e => e.SourcePublicId).HasDatabaseName("IX_ACC_LendingPostings_Source");
        builder.HasIndex(e => e.RelatedMessagePublicId).HasDatabaseName("IX_ACC_LendingPostings_Related");
        builder.HasIndex(e => e.AccountingDocumentId).HasDatabaseName("IX_ACC_LendingPostings_AccountingDocument");
        builder.HasIndex(e => e.BatchPublicId).HasDatabaseName("IX_ACC_LendingPostings_Batch");
        builder.HasIndex(e => e.OperationDate).HasDatabaseName("IX_ACC_LendingPostings_OperationDate");
        builder.HasIndex(e => e.ActorUserId).HasDatabaseName("IX_ACC_LendingPostings_ActorUser");
    }
}

/// <summary>
/// <c>ACC_RegulatoryIndicators</c>: hecho de sólo inserción. Índice <c>(Kind, CutoffDate)</c> para «el último no reemplazado con corte
/// ≤ fecha» y <c>UK_ACC_RegulatoryIndicators_Supersedes</c> filtrado no nulo: un indicador se reemplaza una sola vez (C121).
/// </summary>
public class RegulatoryIndicatorConfiguration : IEntityTypeConfiguration<RegulatoryIndicator>
{
    public void Configure(EntityTypeBuilder<RegulatoryIndicator> builder)
    {
        builder.ToTable("ACC_RegulatoryIndicators");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_RegulatoryIndicators_PublicId");

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.CutoffDate).IsRequired();
        builder.Property(e => e.Value).HasPrecision(20, 6).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.RegisteredByUserId).IsRequired();
        builder.Property(e => e.RegisteredByName).HasMaxLength(150).IsRequired();

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Kind, e.CutoffDate }).HasDatabaseName("IX_ACC_RegulatoryIndicators_Kind_Cutoff");
        builder.HasIndex(e => e.SupersedesPublicId).IsUnique()
            .HasDatabaseName("UK_ACC_RegulatoryIndicators_Supersedes").HasFilter("[SupersedesPublicId] IS NOT NULL");
        builder.HasIndex(e => e.RegisteredByUserId).HasDatabaseName("IX_ACC_RegulatoryIndicators_RegisteredByUser");
    }
}
