using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary><c>LND_UserCreditLineScopes</c> (feature 013, T0210; data-model §3.14): único <c>(UserId, CreditLineId)</c> entre vivos.</summary>
public class UserCreditLineScopeConfiguration : IEntityTypeConfiguration<UserCreditLineScope>
{
    public const string Tabla = "LND_UserCreditLineScopes";

    public void Configure(EntityTypeBuilder<UserCreditLineScope> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.UserId, e.CreditLineId }).IsUnique()
            .HasDatabaseName("UK_LND_UserCreditLineScopes_User_Line").HasFilter("[IsDeleted] = 0");
    }
}
