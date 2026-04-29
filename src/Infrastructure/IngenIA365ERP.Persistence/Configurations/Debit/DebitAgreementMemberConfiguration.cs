using IngenIA365ERP.Domain.Entities.Debit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Debit;

public class DebitAgreementMemberConfiguration : IEntityTypeConfiguration<DebitAgreementMember>
{
    public void Configure(EntityTypeBuilder<DebitAgreementMember> builder)
    {
        builder.ToTable("DEB_AgreementMembers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.UserName).HasMaxLength(20).IsRequired();
        builder.Property(e => e.VoucherCode).HasMaxLength(5).IsRequired();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
