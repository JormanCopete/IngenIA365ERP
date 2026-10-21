using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary>
/// <c>LND_LoanNumberReservations</c> (feature 013; data-model §2.11): número y clave de idempotencia únicos, sin filtro; el pago de
/// Comercial va como dato con su índice.
/// </summary>
public class LoanNumberReservationConfiguration : IEntityTypeConfiguration<LoanNumberReservation>
{
    public const string Tabla = "LND_LoanNumberReservations";

    public void Configure(EntityTypeBuilder<LoanNumberReservation> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(e => e.RequestSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.SalesDocumentPublicId).IsRequired();
        builder.Property(e => e.PaymentPublicId).IsRequired();
        builder.Property(e => e.ConsultationEvidence).HasMaxLength(64);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ReservedAt).IsRequired();
        builder.Property(e => e.ExpiresOn).IsRequired();
        builder.Property(e => e.UsedAt);
        builder.Property(e => e.ReleasedAt);
        builder.Property(e => e.ReleaseReason).HasMaxLength(300);
        builder.Property(e => e.ExpiredAt);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLimit>().WithMany().HasForeignKey(e => e.CreditLimitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReservedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReleasedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_LoanNumberReservations_Number");
        builder.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName("UK_LND_LoanNumberReservations_Key");
        builder.HasIndex(e => e.PaymentPublicId).HasDatabaseName("IX_LND_LoanNumberReservations_PaymentPublicId");
        builder.HasIndex(e => new { e.PersonId, e.Status }).HasDatabaseName("IX_LND_LoanNumberReservations_Person_Status");
        builder.HasIndex(e => new { e.Status, e.ExpiresOn }).HasDatabaseName("IX_LND_LoanNumberReservations_Status_ExpiresOn");
    }
}
