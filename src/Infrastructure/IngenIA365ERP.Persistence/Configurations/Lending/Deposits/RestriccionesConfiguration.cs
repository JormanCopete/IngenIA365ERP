using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

/// <summary>
/// <c>LND_AccountHolds</c> (feature 014, US11, T187; data-model §5.1): el índice de los vigentes por cuenta (filtrado a los no levantados) y por embargo.
/// Migración <c>CaptacionesAportes</c> (regenerable hasta el Polish).
/// </summary>
public class AccountHoldConfiguration : IEntityTypeConfiguration<AccountHold>
{
    public const string Tabla = "LND_AccountHolds";

    public void Configure(EntityTypeBuilder<AccountHold> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_AccountHolds_Amount", "[Amount] IS NULL OR [Amount] > 0"));

        builder.Property(e => e.Reason).HasConversion<int>().IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(AccountHold.LargoDeLasNotas);
        builder.Property(e => e.ReleaseReason).HasMaxLength(AccountHold.LargoDelMotivo);

        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Collateral>().WithMany().HasForeignKey(e => e.CollateralId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Garnishment>().WithMany().HasForeignKey(e => e.GarnishmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.PlacedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReleasedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MemberAccountId).HasDatabaseName("IX_LND_AccountHolds_Live").HasFilter("[ReleasedAt] IS NULL");
        builder.HasIndex(e => e.GarnishmentId).HasDatabaseName("IX_LND_AccountHolds_Garnishment").HasFilter("[GarnishmentId] IS NOT NULL");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_AccountHolds_Loan").HasFilter("[LoanId] IS NOT NULL");
    }
}

/// <summary><c>LND_Garnishments</c> (feature 014, US11, T187; data-model §5.2): índice por persona y estado. Migración <c>CaptacionesAportes</c>.</summary>
public class GarnishmentConfiguration : IEntityTypeConfiguration<Garnishment>
{
    public const string Tabla = "LND_Garnishments";

    public void Configure(EntityTypeBuilder<Garnishment> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_Garnishments_Amount", "[Amount] IS NULL OR [Amount] > 0"));

        builder.Property(e => e.Authority).HasConversion<int>().IsRequired();
        builder.Property(e => e.CourtName).HasMaxLength(Garnishment.LargoDelNombre).IsRequired();
        builder.Property(e => e.CourtCode).HasMaxLength(Garnishment.LargoDelCodigo);
        builder.Property(e => e.ProcessNumber).HasMaxLength(Garnishment.LargoDelRadicado).IsRequired();
        builder.Property(e => e.OrderNumber).HasMaxLength(Garnishment.LargoDelRadicado).IsRequired();
        builder.Property(e => e.OrderDate).IsRequired();
        builder.Property(e => e.ReceivedOn).IsRequired();
        builder.Property(e => e.ClaimantName).HasMaxLength(Garnishment.LargoDelNombre).IsRequired();
        builder.Property(e => e.ClaimantTaxId).HasMaxLength(Garnishment.LargoDelDocumento);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.AccountsScope).HasConversion<int>().IsRequired();
        builder.Property(e => e.DestinationBankAccountNumber).HasMaxLength(Garnishment.LargoDeLaCuentaBancaria);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(Garnishment.LargoDeLasNotas);
        builder.Property(e => e.ReleaseOrderNumber).HasMaxLength(Garnishment.LargoDelRadicado);
        builder.Property(e => e.ReleaseReason).HasMaxLength(Garnishment.LargoDeLasNotas);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.CourtPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.ClaimantPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.DestinationBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReleasedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PersonId, e.Status }).HasDatabaseName("IX_LND_Garnishments_Person_Status");
    }
}

/// <summary><c>LND_GarnishmentResponses</c> (feature 014, US11, T187; data-model §5.2.1): hecho por embargo. Migración <c>CaptacionesAportes</c>.</summary>
public class GarnishmentResponseConfiguration : IEntityTypeConfiguration<GarnishmentResponse>
{
    public const string Tabla = "LND_GarnishmentResponses";

    public void Configure(EntityTypeBuilder<GarnishmentResponse> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.SentOn).IsRequired();
        builder.Property(e => e.RetainedAmount).Monto();
        builder.Property(e => e.ConsignedAmount).Monto();
        builder.Property(e => e.Text).HasMaxLength(GarnishmentResponse.LargoDelTexto);
        builder.Property(e => e.RespondedAt).IsRequired();

        builder.HasOne<Garnishment>().WithMany().HasForeignKey(e => e.GarnishmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IssuedLendingDocument>().WithMany().HasForeignKey(e => e.IssuedDocumentId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_GarnishmentResponses_IssuedDocument");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RespondedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.GarnishmentId).HasDatabaseName("IX_LND_GarnishmentResponses_Garnishment");
    }
}
