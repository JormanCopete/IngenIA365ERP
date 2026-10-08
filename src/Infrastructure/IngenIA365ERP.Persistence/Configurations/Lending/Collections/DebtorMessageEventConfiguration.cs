using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Collections;

/// <summary>
/// <c>LND_DebtorMessageEvents</c> (feature 013, C.3; data-model §26.2): hecho <c>bigint</c>. Único <c>(DebtorMessageId, Sequence)</c>
/// y la cola en dos índices filtrados (<c>Queued</c>, <c>Deferred</c>) porque un filtro de índice no usa <c>IN</c> ni <c>OR</c>.
/// </summary>
public class DebtorMessageEventConfiguration : IEntityTypeConfiguration<DebtorMessageEvent>
{
    public const string Tabla = "LND_DebtorMessageEvents";

    public void Configure(EntityTypeBuilder<DebtorMessageEvent> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);

        builder.Property(e => e.Sequence).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.ReasonCode).HasMaxLength(80);
        builder.Property(e => e.ReasonText).HasMaxLength(500);
        builder.Property(e => e.DestinationAddress).HasMaxLength(320);
        builder.Property(e => e.ProviderReference).HasMaxLength(120);
        builder.Property(e => e.ExternalOutcome).HasConversion<int?>();
        builder.Property(e => e.ActorKind).HasConversion<int>().IsRequired();

        builder.HasOne(e => e.DebtorMessage).WithMany().HasForeignKey(e => e.DebtorMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DebtorMessageId, e.Sequence }).IsUnique()
            .HasDatabaseName("UK_LND_DebtorMessageEvents_Message_Sequence");
        builder.HasIndex(e => new { e.Kind, e.DueAt }, "IX_LND_DebtorMessageEvents_Queued").HasFilter("[Kind] = 1");
        builder.HasIndex(e => new { e.Kind, e.DueAt }, "IX_LND_DebtorMessageEvents_Deferred").HasFilter("[Kind] = 2");
    }
}
