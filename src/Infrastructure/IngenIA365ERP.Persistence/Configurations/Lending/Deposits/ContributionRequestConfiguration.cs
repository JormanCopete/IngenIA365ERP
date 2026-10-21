using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

/// <summary>
/// <c>LND_ContributionRequests</c> (feature 014, US3, T086; research E58): la devolución de aportes sin retiro y la cesión entre asociados, con su estado,
/// quién la pidió y quién la decidió, y la operación (y la orden) que nacieron al aprobarla. Migración <c>CaptacionesAportes</c>.
/// </summary>
public class ContributionRequestConfiguration : IEntityTypeConfiguration<ContributionRequest>
{
    public const string Tabla = "LND_ContributionRequests";

    public void Configure(EntityTypeBuilder<ContributionRequest> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_ContributionRequests_Amount", "[Amount] > 0"));

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.RefundCause).HasConversion<int?>();
        builder.Property(e => e.Reason).HasMaxLength(ContributionRequest.LargoDelMotivo).IsRequired();
        builder.Property(e => e.DecisionReason).HasMaxLength(ContributionRequest.LargoDelMotivo);
        builder.Property(e => e.RequestedAt).IsRequired();

        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.TargetMemberAccountId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_ContributionRequests_TargetAccount");
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.TargetPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountPayoutOrder>().WithMany().HasForeignKey(e => e.PayoutOrderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Status, e.Kind }).HasDatabaseName("IX_LND_ContributionRequests_Status_Kind");
        builder.HasIndex(e => e.MemberAccountId).HasDatabaseName("IX_LND_ContributionRequests_MemberAccountId");
        builder.HasIndex(e => e.OperationId).IsUnique().HasDatabaseName("UK_LND_ContributionRequests_Operation").HasFilter("[OperationId] IS NOT NULL");
    }
}
