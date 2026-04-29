using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class DefaultRecordConfiguration : IEntityTypeConfiguration<DefaultRecord>
{
    public void Configure(EntityTypeBuilder<DefaultRecord> builder)
    {
        builder.ToTable("LND_DefaultRecords");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_DefaultRecords_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.IsManaged).HasMaxLength(2).IsRequired();
        builder.Property(e => e.LegacyCodigoTer).HasMaxLength(20);
        builder.Property(e => e.CapitalBalance).HasPrecision(18, 2);
        builder.Property(e => e.ExtraBalance).HasPrecision(18, 2);
        builder.Property(e => e.InterestBalance).HasPrecision(18, 2);
        builder.Property(e => e.DefaultBalance).HasPrecision(18, 2);
        builder.Property(e => e.InsuranceBalance).HasPrecision(18, 2);
        builder.Property(e => e.AdminBalance).HasPrecision(18, 2);
        builder.Property(e => e.OtherBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorCapitalBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorExtraBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorInterestBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorDefaultBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorInsuranceBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorAdminBalance).HasPrecision(18, 2);
        builder.Property(e => e.PriorOtherBalance).HasPrecision(18, 2);
        builder.Property(e => e.AccruedCapital).HasPrecision(18, 2);
        builder.Property(e => e.AccruedExtra).HasPrecision(18, 2);
        builder.Property(e => e.AccruedInterest).HasPrecision(18, 2);
        builder.Property(e => e.AccruedDefault).HasPrecision(18, 2);
        builder.Property(e => e.AccruedInsurance).HasPrecision(18, 2);
        builder.Property(e => e.AccruedAdmin).HasPrecision(18, 2);
        builder.Property(e => e.AccruedOther).HasPrecision(18, 2);
        builder.Property(e => e.PaidCapital).HasPrecision(18, 2);
        builder.Property(e => e.PaidExtra).HasPrecision(18, 2);
        builder.Property(e => e.PaidInterest).HasPrecision(18, 2);
        builder.Property(e => e.PaidDefault).HasPrecision(18, 2);
        builder.Property(e => e.PaidInsurance).HasPrecision(18, 2);
        builder.Property(e => e.PaidAdmin).HasPrecision(18, 2);
        builder.Property(e => e.PaidOther).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
