using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class CheckClearingConfiguration : IEntityTypeConfiguration<CheckClearing>
{
    public void Configure(EntityTypeBuilder<CheckClearing> builder)
    {
        builder.ToTable("LND_CheckClearing");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_CheckClearing_PublicId");

        builder.Property(e => e.Plaza).HasMaxLength(2).IsRequired();
        builder.Property(e => e.BankCode).HasMaxLength(5);
        builder.Property(e => e.Status).HasMaxLength(2).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(20);
        builder.Property(e => e.CheckNumber).HasPrecision(18, 2);
        builder.Property(e => e.Amount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
