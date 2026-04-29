using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class ContributionReductionConfiguration : IEntityTypeConfiguration<ContributionReduction>
{
    public void Configure(EntityTypeBuilder<ContributionReduction> builder)
    {
        builder.ToTable("LND_ContributionReductions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_ContributionReductions_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.OpeningBalance).HasPrecision(15, 2);
        builder.Property(e => e.Average).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance1).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance2).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance3).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance4).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance5).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance6).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance7).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance8).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance9).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance10).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance11).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance12).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance13).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance14).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance15).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance16).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance17).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance18).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance19).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance20).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance21).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance22).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance23).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance24).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance25).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance26).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance27).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance28).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance29).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance30).HasPrecision(15, 2);
        builder.Property(e => e.DayBalance31).HasPrecision(15, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
