using IngenIA365ERP.Domain.Entities.Lending.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Integration;

/// <summary>
/// <c>LND_InboundMessageEvaluations</c> (feature 013, C.3; data-model §21.2): hecho. Único <c>(InboundMessageId, EvaluationNumber)</c>
/// sin filtro; <c>PrevalidationOutcome</c> como texto.
/// </summary>
public class LendingInboundMessageEvaluationConfiguration : IEntityTypeConfiguration<LendingInboundMessageEvaluation>
{
    public const string Tabla = "LND_InboundMessageEvaluations";

    public void Configure(EntityTypeBuilder<LendingInboundMessageEvaluation> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.EvaluationNumber).IsRequired();
        builder.Property(e => e.AsOfDate).IsRequired();
        builder.Property(e => e.EvaluatedAt).IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>().IsRequired();
        builder.Property(e => e.CreditLineCode).HasMaxLength(10);
        builder.Property(e => e.ChecksJson).IsRequired();
        builder.Property(e => e.FailureCode).HasMaxLength(80);
        builder.Property(e => e.FailureReason).HasMaxLength(500);
        builder.Property(e => e.Retryable).IsRequired();
        builder.Property(e => e.PrevalidationOutcome).HasMaxLength(20);

        builder.HasOne<LendingInboundMessage>().WithMany().HasForeignKey(e => e.InboundMessageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.InboundMessageId, e.EvaluationNumber }).IsUnique()
            .HasDatabaseName("UK_LND_InboundMessageEvaluations_Message_Number");
    }
}
