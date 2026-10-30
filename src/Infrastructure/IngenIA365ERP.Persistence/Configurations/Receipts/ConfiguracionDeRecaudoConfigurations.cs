using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Vaults;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, US1 (T0071; data-model §1, §2.6): las tablas de configuración de Recaudo. Catálogos con código: UK filtrado por IsDeleted (el código es
// la nomenclatura de la cooperativa, CodigoDeCatalogo). FK Restrict sólo a la plataforma (COR_Branches, COR_PaymentMeans, COR_People, SEC_Users) y
// entre tablas RCD_. Valores por alias de PrecisionDeRecaudo. Enums como int.

/// <summary><c>RCD_CollectionPoints</c>. (nuevo)</summary>
public class CollectionPointConfiguration : IEntityTypeConfiguration<CollectionPoint>
{
    public const string Tabla = "RCD_CollectionPoints";

    public void Configure(EntityTypeBuilder<CollectionPoint> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.DocumentPrefix).HasMaxLength(10);
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Code");
        builder.HasIndex(e => e.BranchId).HasDatabaseName($"IX_{Tabla}_BranchId");
    }
}

/// <summary><c>RCD_CashBoxes</c>: código único por punto entre vivas. (nuevo)</summary>
public class CashBoxConfiguration : IEntityTypeConfiguration<CashBox>
{
    public const string Tabla = "RCD_CashBoxes";

    public void Configure(EntityTypeBuilder<CashBox> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CollectionPoint>().WithMany().HasForeignKey(e => e.CollectionPointId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.BaseMode).HasConversion<int>();
        builder.Property(e => e.BaseAmount).Monto();
        builder.Property(e => e.CashWarnCap).Monto();
        builder.Property(e => e.CashBlockCap).Monto();
        builder.Property(e => e.DailyCashOutCap).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => new { e.CollectionPointId, e.Code }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_CollectionPointId_Code");
    }
}

/// <summary><c>RCD_CashBoxCashiers</c>: UK <c>(CashBoxId, UserId, ValidFrom)</c>. (nuevo)</summary>
public class CashBoxCashierConfiguration : IEntityTypeConfiguration<CashBoxCashier>
{
    public const string Tabla = "RCD_CashBoxCashiers";

    public void Configure(EntityTypeBuilder<CashBoxCashier> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CashBox>().WithMany().HasForeignKey(e => e.CashBoxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CashBoxId, e.UserId, e.ValidFrom }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_CashBoxId_UserId_ValidFrom");
        builder.HasIndex(e => e.UserId).HasDatabaseName($"IX_{Tabla}_UserId");
    }
}

/// <summary><c>RCD_PaymentMeansCollectionPoints</c>. (nuevo)</summary>
public class PaymentMeansCollectionPointConfiguration : IEntityTypeConfiguration<PaymentMeansCollectionPoint>
{
    public const string Tabla = "RCD_PaymentMeansCollectionPoints";

    public void Configure(EntityTypeBuilder<PaymentMeansCollectionPoint> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CollectionPoint>().WithMany().HasForeignKey(e => e.CollectionPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CollectionPointId, e.PaymentMeansId, e.ValidFrom }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_Point_Means_From");
    }
}

/// <summary><c>RCD_DocumentTypes</c>. (nuevo)</summary>
public class ReceiptDocumentTypeConfiguration : IEntityTypeConfiguration<ReceiptDocumentType>
{
    public const string Tabla = "RCD_DocumentTypes";

    public void Configure(EntityTypeBuilder<ReceiptDocumentType> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Code");
    }
}

/// <summary><c>RCD_MiscConcepts</c>. (nuevo)</summary>
public class MiscConceptConfiguration : IEntityTypeConfiguration<MiscConcept>
{
    public const string Tabla = "RCD_MiscConcepts";

    public void Configure(EntityTypeBuilder<MiscConcept> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Direction).HasConversion<int>();
        builder.Property(e => e.MaxAmount).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Code");
    }
}

/// <summary><c>RCD_ContingencyBooks</c>. (nuevo)</summary>
public class ContingencyBookConfiguration : IEntityTypeConfiguration<ContingencyBook>
{
    public const string Tabla = "RCD_ContingencyBooks";

    public void Configure(EntityTypeBuilder<ContingencyBook> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CollectionPoint>().WithMany().HasForeignKey(e => e.CollectionPointId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.RegisteredBy).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => new { e.CollectionPointId, e.FromNumber }).HasDatabaseName($"IX_{Tabla}_CollectionPointId_FromNumber");
    }
}

/// <summary><c>RCD_SupervisorDelegations</c>. (nuevo)</summary>
public class SupervisorDelegationConfiguration : IEntityTypeConfiguration<SupervisorDelegation>
{
    public const string Tabla = "RCD_SupervisorDelegations";

    public void Configure(EntityTypeBuilder<SupervisorDelegation> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CollectionPoint>().WithMany().HasForeignKey(e => e.CollectionPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SupervisorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DelegateUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(e => new { e.CollectionPointId, e.DelegateUserId }).HasDatabaseName($"IX_{Tabla}_CollectionPointId_DelegateUserId");
    }
}

/// <summary><c>RCD_ChannelModalityRules</c>. (nuevo)</summary>
public class ChannelModalityRuleConfiguration : IEntityTypeConfiguration<ChannelModalityRule>
{
    public const string Tabla = "RCD_ChannelModalityRules";

    public void Configure(EntityTypeBuilder<ChannelModalityRule> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.Property(e => e.Channel).HasConversion<int>();
        builder.Property(e => e.Target).HasConversion<int>();
        builder.HasIndex(e => new { e.Channel, e.Target, e.Priority }).HasDatabaseName($"IX_{Tabla}_Channel_Target_Priority");
    }
}

/// <summary><c>RCD_Vaults</c>: una por oficina (<c>UK_RCD_Vaults_BranchId</c> entre vivas). (nuevo)</summary>
public class VaultConfiguration : IEntityTypeConfiguration<Vault>
{
    public const string Tabla = "RCD_Vaults";

    public void Configure(EntityTypeBuilder<Vault> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBox>().WithMany().HasForeignKey(e => e.MainCashBoxId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Mode).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => e.BranchId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_BranchId");
    }
}

/// <summary><c>RCD_VaultCustodians</c>. (nuevo)</summary>
public class VaultCustodianConfiguration : IEntityTypeConfiguration<VaultCustodian>
{
    public const string Tabla = "RCD_VaultCustodians";

    public void Configure(EntityTypeBuilder<VaultCustodian> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Vault>().WithMany().HasForeignKey(e => e.VaultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.VaultId, e.UserId, e.ValidFrom }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_VaultId_UserId_ValidFrom");
    }
}

/// <summary>
/// <c>RCD_TransferAccounts</c> (segunda ronda del 2026-10-27, D-f; FR-231): la cuenta propia habilitada para un medio en un punto (o en una caja del punto). Única viva
/// por punto, caja, medio y cuenta. (nuevo)
/// </summary>
public class TransferAccountConfiguration : IEntityTypeConfiguration<TransferAccount>
{
    public const string Tabla = "RCD_TransferAccounts";

    public void Configure(EntityTypeBuilder<TransferAccount> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CollectionPoint>().WithMany().HasForeignKey(e => e.CollectionPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBox>().WithMany().HasForeignKey(e => e.CashBoxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.AccountLabel).HasMaxLength(150).IsRequired();
        builder.HasIndex(e => new { e.CollectionPointId, e.PaymentMeansId }).HasDatabaseName($"IX_{Tabla}_CollectionPointId_PaymentMeansId");
    }
}
