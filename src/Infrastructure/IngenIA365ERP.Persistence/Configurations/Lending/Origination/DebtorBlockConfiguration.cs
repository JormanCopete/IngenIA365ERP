using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary>
/// <c>LND_DebtorBlocks</c> (feature 013; data-model §2.12): un solo bloqueo vigente (sin levantar) por persona, motivo y disparador.
/// </summary>
public class DebtorBlockConfiguration : IEntityTypeConfiguration<DebtorBlock>
{
    public const string Tabla = "LND_DebtorBlocks";

    public void Configure(EntityTypeBuilder<DebtorBlock> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Reason).HasConversion<int>().IsRequired();
        builder.Property(e => e.IsAutomatic).IsRequired();
        builder.Property(e => e.SourceKey).HasMaxLength(60).IsRequired();
        builder.Property(e => e.StartedOn).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.SupportAttachmentPublicId);
        builder.Property(e => e.LiftedOn);
        builder.Property(e => e.LiftedAt);
        builder.Property(e => e.LiftReason).HasMaxLength(500);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.LiftedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PersonId, e.Reason, e.SourceKey }).IsUnique()
            .HasDatabaseName("UK_LND_DebtorBlocks_Active").HasFilter("[LiftedAt] IS NULL");
    }
}
