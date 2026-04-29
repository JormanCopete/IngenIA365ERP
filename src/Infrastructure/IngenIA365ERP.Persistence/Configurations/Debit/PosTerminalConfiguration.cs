using IngenIA365ERP.Domain.Entities.Debit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Debit;

public class PosTerminalConfiguration : IEntityTypeConfiguration<PosTerminal>
{
    public void Configure(EntityTypeBuilder<PosTerminal> builder)
    {
        builder.ToTable("DEB_PosTerminals");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.TerminalCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.VoucherCode).HasMaxLength(5);
        builder.Property(e => e.MerchantName).HasMaxLength(100);
        builder.Property(e => e.Location).HasMaxLength(100);
        builder.Property(e => e.Status).HasMaxLength(2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
