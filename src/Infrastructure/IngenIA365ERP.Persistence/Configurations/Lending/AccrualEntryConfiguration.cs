using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class AccrualEntryConfiguration : IEntityTypeConfiguration<AccrualEntry>
{
    public void Configure(EntityTypeBuilder<AccrualEntry> builder)
    {
        builder.ToTable("LND_AccrualEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_AccrualEntries_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EntryType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Periodicity).HasMaxLength(2).IsRequired();
        builder.Property(e => e.AffectsCapital).HasMaxLength(2).IsRequired();
        builder.Property(e => e.AffectsInterest).HasMaxLength(2).IsRequired();
        builder.Property(e => e.AffectsExtras).HasMaxLength(2).IsRequired();
        builder.Property(e => e.FixedInstallments).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Authorization).HasMaxLength(20).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(2);
        builder.Property(e => e.UserFullName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.AppliesExtras).HasMaxLength(2).IsRequired();
        builder.Property(e => e.AppliesToSavings).HasMaxLength(2);
        builder.Property(e => e.AppliesToServices).HasMaxLength(2);
        builder.Property(e => e.LegacyCodigoTer).HasMaxLength(20);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
