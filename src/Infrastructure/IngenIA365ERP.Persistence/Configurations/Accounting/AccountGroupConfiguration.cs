using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class AccountGroupConfiguration : IEntityTypeConfiguration<AccountGroup>
{
    public void Configure(EntityTypeBuilder<AccountGroup> builder)
    {
        builder.ToTable("ACC_AccountGroups");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_AccountGroups_PublicId");

        builder.Property(e => e.AccountCode).HasMaxLength(15);
        builder.Property(e => e.Description).HasMaxLength(100);
        builder.Property(e => e.FinancialStatementCode).HasMaxLength(5);
        builder.Property(e => e.SpecialCode).HasMaxLength(2);

        builder.HasMany(e => e.ChildGroups).WithOne().HasForeignKey(e => e.ParentGroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Subgroups).WithOne().HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Restrict);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
