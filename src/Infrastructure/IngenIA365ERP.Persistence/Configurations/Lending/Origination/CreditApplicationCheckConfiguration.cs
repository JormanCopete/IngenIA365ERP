using IngenIA365ERP.Domain.Entities.Lending.Origination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary>
/// <c>LND_CreditApplicationChecks</c> (feature 013; data-model §2.4): hecho de validaciones; el índice por solicitud, clase, momento y
/// fecha encuentra la vigente.
/// </summary>
public class CreditApplicationCheckConfiguration : IEntityTypeConfiguration<CreditApplicationCheck>
{
    public const string Tabla = "LND_CreditApplicationChecks";

    public void Configure(EntityTypeBuilder<CreditApplicationCheck> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Stage).HasConversion<int>().IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>().IsRequired();
        builder.Property(e => e.IsExceptionable).IsRequired();
        builder.Property(e => e.ErrorCode).HasMaxLength(80);
        builder.Property(e => e.Detail);
        builder.Property(e => e.EvidenceSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.EvaluatedAt).IsRequired();

        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditApplicationParty>().WithMany().HasForeignKey(e => e.PartyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScreeningResult>().WithMany().HasForeignKey(e => e.ScreeningResultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BureauQuery>().WithMany().HasForeignKey(e => e.BureauQueryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditScore>().WithMany().HasForeignKey(e => e.CreditScoreId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ApplicationId, e.Kind, e.Stage, e.EvaluatedAt })
            .HasDatabaseName("IX_LND_CreditApplicationChecks_App_Kind_Stage");
    }
}
