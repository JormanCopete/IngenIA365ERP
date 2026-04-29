using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class CommitteeMemberConfiguration : IEntityTypeConfiguration<CommitteeMember>
{
    public void Configure(EntityTypeBuilder<CommitteeMember> builder)
    {
        builder.ToTable("COR_CommitteeMembers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_CommitteeMembers_PublicId");

        builder.Property(e => e.PersonId).IsRequired();
        builder.Property(e => e.CommitteeId).IsRequired();
        builder.Property(e => e.LegacyUser).HasMaxLength(100);

        // Unique: person + committee
        builder.HasIndex(e => new { e.PersonId, e.CommitteeId }).IsUnique().HasDatabaseName("UK_COR_CommitteeMembers_Person_Committee");

        // FK indexes
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_COR_CommitteeMembers_PersonId");
        builder.HasIndex(e => e.CommitteeId).HasDatabaseName("IX_COR_CommitteeMembers_CommitteeId");

        // Relationships
        builder.HasOne(e => e.Committee).WithMany(c => c.Members).HasForeignKey(e => e.CommitteeId).OnDelete(DeleteBehavior.Restrict);
        // Person relationship configured from PersonConfiguration

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
