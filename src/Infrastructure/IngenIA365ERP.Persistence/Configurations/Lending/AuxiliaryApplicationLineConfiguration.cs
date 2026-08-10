using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class AuxiliaryApplicationLineConfiguration : IEntityTypeConfiguration<AuxiliaryApplicationLine>
{
    public void Configure(EntityTypeBuilder<AuxiliaryApplicationLine> builder)
    {
        builder.ToTable("LND_AuxiliaryApplicationLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_AuxiliaryApplicationLines_PublicId");

        builder.Property(e => e.LineCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 3);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
