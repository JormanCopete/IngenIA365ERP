using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

// Feature 015, US8 (T0256; data-model §17.1, §17.2): las seis tablas ACC_ de las matrices de Recaudo y Tesorería, en el par IntegracionContableDeRecaudoYTesoreria.
// Molde: LendingIntegrationConfigurations (013). Sin llave a tablas RCD_ ni TES_.

/// <summary>Lo común de las dos matrices: <c>UK (DimensionKey, ValidFrom)</c> filtrado a vivas, índice de resolución y llaves <c>Restrict</c>. (nuevo)</summary>
public abstract class ReglaContableDeModuloConfiguration<T> : IEntityTypeConfiguration<T> where T : ReglaContableDeModulo
{
    protected abstract string Tabla { get; }

    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.ToTable(Tabla);
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_PublicId");

        builder.Property(e => e.FactType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Role).HasMaxLength(40).IsRequired();
        builder.Property(e => e.BranchCode).HasMaxLength(20);
        builder.Property(e => e.CollectionPointCode).HasMaxLength(20);
        builder.Property(e => e.CashBoxCode).HasMaxLength(20);
        builder.Property(e => e.MeansCode).HasMaxLength(20);
        builder.Property(e => e.MiscConceptCode).HasMaxLength(20);
        builder.Property(e => e.TargetModule).HasMaxLength(3);
        builder.Property(e => e.TargetNature).HasMaxLength(20);
        builder.Property(e => e.CollectorClass).HasMaxLength(20);
        builder.Property(e => e.OwnBankAccountCode).HasMaxLength(20);
        builder.Property(e => e.OrderOrigin).HasMaxLength(30);
        builder.Property(e => e.OrderKind).HasMaxLength(40);
        builder.Property(e => e.Cause).HasMaxLength(40);
        builder.Property(e => e.DimensionKey).HasMaxLength(400).IsRequired();
        builder.Property(e => e.SpecificityWeight).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(300).IsRequired();

        builder.HasOne<ChartOfAccount>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCenter>().WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DimensionKey, e.ValidFrom }).IsUnique().HasDatabaseName($"UK_{Tabla}_DimensionKey_ValidFrom").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.FactType, e.Role, e.ValidFrom }).HasDatabaseName($"IX_{Tabla}_FactType_Role");
        builder.HasIndex(e => e.AccountId).HasDatabaseName($"IX_{Tabla}_Account");
        builder.HasIndex(e => e.CostCenterId).HasDatabaseName($"IX_{Tabla}_CostCenter");
    }
}

public sealed class ReceiptsPostingRuleConfiguration : ReglaContableDeModuloConfiguration<ReceiptsPostingRule>
{
    protected override string Tabla => "ACC_ReceiptsPostingRules";
}

public sealed class TreasuryPostingRuleConfiguration : ReglaContableDeModuloConfiguration<TreasuryPostingRule>
{
    protected override string Tabla => "ACC_TreasuryPostingRules";
}

/// <summary>Lo común de los dos mapeos de tipo de comprobante: un hecho, una fila viva. (nuevo)</summary>
public abstract class MapeoDeComprobanteDeModuloConfiguration<T> : IEntityTypeConfiguration<T> where T : MapeoDeComprobanteDeModulo
{
    protected abstract string Tabla { get; }

    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.ToTable(Tabla);
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_PublicId");
        builder.Property(e => e.FactType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.ValidatedBy).HasMaxLength(150);
        builder.HasOne(e => e.VoucherType).WithMany().HasForeignKey(e => e.VoucherTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CrossDocumentType).WithMany().HasForeignKey(e => e.CrossDocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.FactType).IsUnique().HasDatabaseName($"UK_{Tabla}_FactType").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.VoucherTypeId).HasDatabaseName($"IX_{Tabla}_VoucherType");
        builder.HasIndex(e => e.CrossDocumentTypeId).HasDatabaseName($"IX_{Tabla}_CrossDocumentType");
    }
}

public sealed class ReceiptsVoucherMappingConfiguration : MapeoDeComprobanteDeModuloConfiguration<ReceiptsVoucherMapping>
{
    protected override string Tabla => "ACC_ReceiptsVoucherMappings";
}

public sealed class TreasuryVoucherMappingConfiguration : MapeoDeComprobanteDeModuloConfiguration<TreasuryVoucherMapping>
{
    protected override string Tabla => "ACC_TreasuryVoucherMappings";
}

/// <summary>Lo común de los dos recibos de contabilización: único por mensaje, sin filtro (la colisión es «ya procesado»). (nuevo)</summary>
public abstract class RecepcionContableDeModuloConfiguration<T> : IEntityTypeConfiguration<T> where T : RecepcionContableDeModulo
{
    protected abstract string Tabla { get; }

    public void Configure(EntityTypeBuilder<T> builder)
    {
        builder.ToTable(Tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_PublicId");

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
        builder.Property(e => e.OriginActorKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.OriginUserName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ActorKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.ActorName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ProcessedAt).IsRequired();

        builder.HasOne(e => e.AccountingDocument).WithMany().HasForeignKey(e => e.AccountingDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MessagePublicId).IsUnique().HasDatabaseName(IndiceUnico);
        builder.HasIndex(e => e.SourcePublicId).HasDatabaseName($"IX_{Tabla}_Source");
        builder.HasIndex(e => e.RelatedMessagePublicId).HasDatabaseName($"IX_{Tabla}_Related");
        builder.HasIndex(e => e.AccountingDocumentId).HasDatabaseName($"IX_{Tabla}_AccountingDocument");
        builder.HasIndex(e => e.BatchPublicId).HasDatabaseName($"IX_{Tabla}_Batch");
        builder.HasIndex(e => e.ActorUserId).HasDatabaseName($"IX_{Tabla}_ActorUser");
    }

    protected string IndiceUnico => $"UK_{Tabla}_MessagePublicId";
}

public sealed class ReceiptsPostingConfiguration : RecepcionContableDeModuloConfiguration<ReceiptsPosting>
{
    public const string IndiceUnicoDelMensaje = "UK_ACC_ReceiptsPostings_MessagePublicId";

    protected override string Tabla => "ACC_ReceiptsPostings";
}

public sealed class TreasuryPostingConfiguration : RecepcionContableDeModuloConfiguration<TreasuryPosting>
{
    public const string IndiceUnicoDelMensaje = "UK_ACC_TreasuryPostings_MessagePublicId";

    protected override string Tabla => "ACC_TreasuryPostings";
}
