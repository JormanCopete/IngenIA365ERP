using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class AuxiliaryApplicationConfiguration : IEntityTypeConfiguration<AuxiliaryApplication>
{
    public void Configure(EntityTypeBuilder<AuxiliaryApplication> builder)
    {
        builder.ToTable("LND_AuxiliaryApplications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_AuxiliaryApplications_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.LineCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.IsApproved).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(2).IsRequired();
        builder.Property(e => e.IsClosed).HasMaxLength(2).IsRequired();
        builder.Property(e => e.BeneficiaryId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.RequestedAmount).HasPrecision(18, 2);
        builder.Property(e => e.ApprovedAmount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
