using IngenIA365ERP.Domain.Entities.Debit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Debit;

public class DebitAgreementParameterConfiguration : IEntityTypeConfiguration<DebitAgreementParameter>
{
    public void Configure(EntityTypeBuilder<DebitAgreementParameter> builder)
    {
        builder.ToTable("DEB_AgreementParameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.AgreementCode).IsUnique();

        builder.Property(e => e.AgreementCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
