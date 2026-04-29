using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class SalespersonConfiguration : IEntityTypeConfiguration<Salesperson>
{
    public void Configure(EntityTypeBuilder<Salesperson> builder)
    {
        builder.ToTable("INV_Salespeople");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.IdNumber).IsUnique();

        builder.Property(e => e.IdNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(100);
        builder.Property(e => e.Address).HasMaxLength(100);
        builder.Property(e => e.Phone).HasMaxLength(30);
        builder.Property(e => e.Mobile).HasMaxLength(30);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
