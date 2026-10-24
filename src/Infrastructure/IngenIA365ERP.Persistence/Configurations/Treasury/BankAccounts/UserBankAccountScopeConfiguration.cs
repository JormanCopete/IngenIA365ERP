using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.BankAccounts;

/// <summary>
/// <c>TES_UserBankAccountScopes</c> (feature 015, T0057; data-model §9.3): una fila viva por usuario y cuenta propia (UK filtrado por <c>IsDeleted</c>), FK
/// <c>Restrict</c> a <c>SEC_Users</c> y, desde US10 (fase 7, que regenera <c>TesoreriaNucleo</c>), a <c>TES_BankAccounts</c>. (nuevo)
/// </summary>
public class UserBankAccountScopeConfiguration : IEntityTypeConfiguration<UserBankAccountScope>
{
    public const string Tabla = "TES_UserBankAccountScopes";

    public void Configure(EntityTypeBuilder<UserBankAccountScope> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.UserId, e.BankAccountId }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_UserId_BankAccountId");
    }
}
