using IngenIA365ERP.Domain.Entities.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury;

public class CheckConfiguration : IEntityTypeConfiguration<Check>
{
    public void Configure(EntityTypeBuilder<Check> builder)
    {
        builder.ToTable("TRS_Checks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.ConceptCode, e.BankId, e.SequentialNumber }).IsUnique();

        builder.Property(e => e.ConceptCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.VoucherCode).HasMaxLength(5);
        builder.Property(e => e.VoidDetail).HasMaxLength(50);
        builder.Property(e => e.VoidUserId).HasMaxLength(20);
        builder.Property(e => e.VoidVoucherCode).HasMaxLength(5);
        builder.Property(e => e.RecordUserId).HasMaxLength(20);
        builder.Property(e => e.Status).HasMaxLength(2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
