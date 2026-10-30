using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, T0057 (US12; R-30; data-model §1.11): las filas de alcance de Recaudo. Una fila viva por usuario y objeto (UK filtrado por
// IsDeleted: lo retirado queda de baja lógica y se puede volver a asignar). FK Restrict a SEC_Users y al objeto.

/// <summary><c>RCD_UserCollectionPointScopes</c>. (nuevo)</summary>
public class UserCollectionPointScopeConfiguration : IEntityTypeConfiguration<UserCollectionPointScope>
{
    public const string Tabla = "RCD_UserCollectionPointScopes";

    public void Configure(EntityTypeBuilder<UserCollectionPointScope> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CollectionPoint>().WithMany().HasForeignKey(e => e.CollectionPointId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.UserId, e.CollectionPointId }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_UserId_CollectionPointId");
    }
}

/// <summary><c>RCD_UserCashBoxScopes</c>. (nuevo)</summary>
public class UserCashBoxScopeConfiguration : IEntityTypeConfiguration<UserCashBoxScope>
{
    public const string Tabla = "RCD_UserCashBoxScopes";

    public void Configure(EntityTypeBuilder<UserCashBoxScope> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashBox>().WithMany().HasForeignKey(e => e.CashBoxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.UserId, e.CashBoxId }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_UserId_CashBoxId");
    }
}

/// <summary><c>RCD_UserPaymasterScopes</c>. (nuevo)</summary>
public class UserPaymasterScopeConfiguration : IEntityTypeConfiguration<UserPaymasterScope>
{
    public const string Tabla = "RCD_UserPaymasterScopes";

    public void Configure(EntityTypeBuilder<UserPaymasterScope> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PaymasterPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.UserId, e.PaymasterPersonId }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_UserId_PaymasterPersonId");
    }
}
