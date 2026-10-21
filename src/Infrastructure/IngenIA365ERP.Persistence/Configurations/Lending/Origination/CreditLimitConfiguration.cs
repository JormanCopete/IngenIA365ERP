using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary>
/// <c>LND_CreditLimits</c> (feature 013; data-model §2.10): versiones de cupo de sólo inserción; sin único de ordinal (con
/// <c>CreditLineId</c> nulo los motores tratan distinto los nulos): lo garantiza el cerrojo del deudor.
/// </summary>
public class CreditLimitConfiguration : IEntityTypeConfiguration<CreditLimit>
{
    public const string Tabla = "LND_CreditLimits";

    public void Configure(EntityTypeBuilder<CreditLimit> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Origin).HasConversion<int>().IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.CalculationDetail);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.DecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ContributionBalanceLoad>().WithMany().HasForeignKey(e => e.ContributionBalanceLoadId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PersonId, e.Kind, e.CreditLineId, e.ValidFrom })
            .HasDatabaseName("IX_LND_CreditLimits_Person_Kind_Line_ValidFrom");
    }
}
