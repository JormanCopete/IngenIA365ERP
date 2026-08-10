using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class AccountSubgroupConfiguration : IEntityTypeConfiguration<AccountSubgroup>
{
    public void Configure(EntityTypeBuilder<AccountSubgroup> builder)
    {
        builder.ToTable("ACC_AccountSubgroups");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_AccountSubgroups_PublicId");

        builder.Property(e => e.AccountCode).HasMaxLength(15).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(100).IsRequired();
        builder.Property(e => e.FinancialStatementCode).HasMaxLength(5);
        builder.Property(e => e.SpecialCode).HasMaxLength(1);

        builder.HasMany(e => e.ChildSubgroups).WithOne().HasForeignKey(e => e.ParentSubgroupId).OnDelete(DeleteBehavior.Restrict);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
