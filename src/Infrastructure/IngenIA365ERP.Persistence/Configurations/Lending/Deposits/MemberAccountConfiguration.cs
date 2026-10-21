using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

/// <summary>
/// <c>LND_MemberAccounts</c> (feature 014, K7; data-model §3.1): número único sin filtro, naturaleza distinta de colocación, clase obligatoria
/// sólo en captación, una cuenta de aportes viva por persona y un ahorro permanente vivo por persona y línea. Migración <c>CaptacionesAportes</c>.
/// </summary>
public class MemberAccountConfiguration : IEntityTypeConfiguration<MemberAccount>
{
    public const string Tabla = "LND_MemberAccounts";

    public void Configure(EntityTypeBuilder<MemberAccount> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_MemberAccounts_Nature", "[Nature] <> 1");
            t.HasCheckConstraint("CK_LND_MemberAccounts_Class",
                "([Nature] = 2 AND [DepositClass] IS NOT NULL) OR ([Nature] = 3 AND [DepositClass] IS NULL)");
        });

        builder.Property(e => e.Number).HasMaxLength(MemberAccount.LargoDelNumero).IsRequired();
        builder.Property(e => e.Nature).HasConversion<int>().IsRequired();
        builder.Property(e => e.DepositClass).HasConversion<int?>();
        builder.Property(e => e.HandlingMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.StatusChangedAt).IsRequired();
        builder.Property(e => e.Origin).HasConversion<int>().IsRequired();
        builder.Property(e => e.OpenedOn).IsRequired();
        builder.Property(e => e.CloseReason).HasMaxLength(300);
        builder.Property(e => e.IsGarnished).IsRequired();
        builder.Property(e => e.RequiresDataUpdate).IsRequired();
        builder.Property(e => e.ContractualStatus).HasConversion<int?>();
        builder.Property(e => e.AccrualSuspensionReason).HasMaxLength(40);
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(30);
        // US6 (T170, research E135): la propuesta del CDAT que espera el fondeo.
        builder.Property(e => e.PendingTermsJson);
        builder.Property(e => e.MaturityInstructionJson);

        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.HolderPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OpenedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_MemberAccounts_Number");
        // Una cuenta de aportes viva por persona (Nature = Contribution 3, Status ≠ Closed 6); el reingreso abre otra (FR-085).
        builder.HasIndex(e => e.HolderPersonId).IsUnique().HasDatabaseName("UK_LND_MemberAccounts_LiveContribution")
            .HasFilter("[Nature] = 3 AND [Status] <> 6 AND [IsDeleted] = 0");
        // Un ahorro permanente vivo por persona y línea (DepositClass = Permanent 3).
        builder.HasIndex(e => new { e.HolderPersonId, e.CreditLineId }).IsUnique().HasDatabaseName("UK_LND_MemberAccounts_LivePermanent")
            .HasFilter("[DepositClass] = 3 AND [Status] <> 6 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.HolderPersonId, e.Status }).HasDatabaseName("IX_LND_MemberAccounts_Holder_Status");
        builder.HasIndex(e => new { e.CreditLineId, e.Status }).HasDatabaseName("IX_LND_MemberAccounts_Line_Status");
        builder.HasIndex(e => new { e.BranchId, e.Status }).HasDatabaseName("IX_LND_MemberAccounts_Branch_Status");
        builder.HasIndex(e => e.SourceAccountNumber).HasDatabaseName("IX_LND_MemberAccounts_SourceAccountNumber")
            .HasFilter("[SourceAccountNumber] IS NOT NULL");
        builder.HasIndex(e => e.MigrationLoadId).HasDatabaseName("IX_LND_MemberAccounts_MigrationLoadId")
            .HasFilter("[MigrationLoadId] IS NOT NULL");
    }
}

/// <summary>
/// <c>LND_MemberAccountParties</c> (feature 014, K7; data-model §3.2): un titular vigente por cuenta, una fila vigente por persona y papel, y el
/// índice por persona que usa la consulta de productos (FR-095). Proporción como fracción y límite de firma en pesos.
/// </summary>
public class MemberAccountPartyConfiguration : IEntityTypeConfiguration<MemberAccountParty>
{
    public const string Tabla = "LND_MemberAccountParties";

    public void Configure(EntityTypeBuilder<MemberAccountParty> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Role).HasConversion<int>().IsRequired();
        builder.Property(e => e.Share).Fraccion();
        builder.Property(e => e.SignatureLimit).Monto();
        builder.Property(e => e.RepresentationBasis).HasMaxLength(30);
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.EndReason).HasMaxLength(MemberAccountParty.LargoDelMotivo);

        builder.HasOne(e => e.MemberAccount).WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.EndedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MemberAccountId).IsUnique().HasDatabaseName("UK_LND_MemberAccountParties_Holder")
            .HasFilter("[Role] = 1 AND [ValidTo] IS NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.MemberAccountId, e.PersonId, e.Role }).IsUnique().HasDatabaseName("UK_LND_MemberAccountParties_Account_Person_Role")
            .HasFilter("[ValidTo] IS NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.PersonId, e.Role }).HasDatabaseName("IX_LND_MemberAccountParties_Person_Role");
    }
}

/// <summary>
/// <c>LND_AccountPayoutOrders</c> (feature 014, K7; data-model §3.9): una orden por operación, número único, valor positivo e índice de las que
/// esperan resultado.
/// </summary>
public class AccountPayoutOrderConfiguration : IEntityTypeConfiguration<AccountPayoutOrder>
{
    public const string Tabla = "LND_AccountPayoutOrders";

    public void Configure(EntityTypeBuilder<AccountPayoutOrder> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_AccountPayoutOrders_Amount", "[Amount] > 0"));

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Purpose).HasConversion<int>().IsRequired();
        builder.Property(e => e.BeneficiaryTaxIdType).HasMaxLength(10).IsRequired();
        builder.Property(e => e.BeneficiaryTaxId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.BeneficiaryName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.DestinationAccountNumber).HasMaxLength(25);
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.IssuedAt).IsRequired();
        builder.Property(e => e.ResultDueOn).IsRequired();

        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Lending.Deposits.Transactions.MemberAccountOperation>().WithMany().HasForeignKey(e => e.OperationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.BeneficiaryPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.DestinationBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);

        // E258 (respuestas 92 y 96): un regiro comparte la operación de la orden que vuelve a girar; la unicidad rige para las órdenes originales.
        builder.HasIndex(e => e.OperationId).IsUnique().HasDatabaseName("UK_LND_AccountPayoutOrders_Operation")
            .HasFilter("[ReissuesOrderId] IS NULL");
        builder.HasOne<AccountPayoutOrder>().WithMany().HasForeignKey(e => e.ReissuesOrderId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AccountPayoutOrders_ReissuesOrder");
        builder.HasIndex(e => e.ReissuesOrderId).HasDatabaseName("IX_LND_AccountPayoutOrders_ReissuesOrderId").HasFilter("[ReissuesOrderId] IS NOT NULL");
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_AccountPayoutOrders_Number");
        builder.HasIndex(e => new { e.MemberAccountId, e.Status }).HasDatabaseName("IX_LND_AccountPayoutOrders_Account_Status");
        builder.HasIndex(e => new { e.Status, e.ResultDueOn }).HasDatabaseName("IX_LND_AccountPayoutOrders_Status_ResultDueOn");
        // US10 (T096, research E17): la FK nace con LND_MembershipWithdrawals.
        builder.HasOne<Domain.Entities.Lending.Deposits.Processes.MembershipWithdrawal>().WithMany().HasForeignKey(e => e.MembershipWithdrawalId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_LND_AccountPayoutOrders_MembershipWithdrawal");
        builder.HasIndex(e => e.MembershipWithdrawalId).HasDatabaseName("IX_LND_AccountPayoutOrders_MembershipWithdrawalId")
            .HasFilter("[MembershipWithdrawalId] IS NOT NULL");
        // US14 (T207): el caso de fallecimiento cuya entrega paga.
        builder.HasOne<Domain.Entities.Lending.Deposits.Processes.DeceasedHolderCase>().WithMany().HasForeignKey(e => e.DeceasedHolderCaseId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_LND_AccountPayoutOrders_DeceasedHolderCase");
        builder.HasIndex(e => e.DeceasedHolderCaseId).HasDatabaseName("IX_LND_AccountPayoutOrders_DeceasedHolderCase")
            .HasFilter("[DeceasedHolderCaseId] IS NOT NULL");
    }
}

/// <summary>
/// <c>LND_GmfExemptAccounts</c> (feature 014, US2, T072; data-model §3.8): una sola cuenta exenta del GMF vigente por persona
/// (<c>UK_LND_GmfExemptAccounts_LivePerson</c>, filtrado a la vigencia abierta) y el índice por cuenta. Migración <c>CaptacionesAportes</c>.
/// </summary>
public class GmfExemptAccountConfiguration : IEntityTypeConfiguration<GmfExemptAccount>
{
    public const string Tabla = "LND_GmfExemptAccounts";

    public void Configure(EntityTypeBuilder<GmfExemptAccount> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.MarkReason).HasMaxLength(GmfExemptAccount.LargoDelMotivo).IsRequired();
        builder.Property(e => e.EndReason).HasMaxLength(GmfExemptAccount.LargoDelMotivo);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.MarkedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.EndedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.PersonId).IsUnique().HasDatabaseName("UK_LND_GmfExemptAccounts_LivePerson")
            .HasFilter("[ValidTo] IS NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => e.MemberAccountId).HasDatabaseName("IX_LND_GmfExemptAccounts_MemberAccountId");
    }
}
