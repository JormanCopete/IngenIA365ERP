using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class RiskCategoryConfiguration : IEntityTypeConfiguration<RiskCategory>
{
    public void Configure(EntityTypeBuilder<RiskCategory> builder)
    {
        builder.ToTable("ACC_RiskCategories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_RiskCategories_PublicId");

        builder.Property(e => e.AccountCode).HasMaxLength(15).IsRequired();
        builder.Property(e => e.PeriodCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.InitialBalance).HasPrecision(18, 2);
        builder.Property(e => e.Day1).HasPrecision(18, 2);
        builder.Property(e => e.Day2).HasPrecision(18, 2);
        builder.Property(e => e.Day3).HasPrecision(18, 2);
        builder.Property(e => e.Day4).HasPrecision(18, 2);
        builder.Property(e => e.Day5).HasPrecision(18, 2);
        builder.Property(e => e.Day6).HasPrecision(18, 2);
        builder.Property(e => e.Day7).HasPrecision(18, 2);
        builder.Property(e => e.Day8).HasPrecision(18, 2);
        builder.Property(e => e.Day9).HasPrecision(18, 2);
        builder.Property(e => e.Day10).HasPrecision(18, 2);
        builder.Property(e => e.Day11).HasPrecision(18, 2);
        builder.Property(e => e.Day12).HasPrecision(18, 2);
        builder.Property(e => e.Day13).HasPrecision(18, 2);
        builder.Property(e => e.Day14).HasPrecision(18, 2);
        builder.Property(e => e.Day15).HasPrecision(18, 2);
        builder.Property(e => e.Day16).HasPrecision(18, 2);
        builder.Property(e => e.Day17).HasPrecision(18, 2);
        builder.Property(e => e.Day18).HasPrecision(18, 2);
        builder.Property(e => e.Day19).HasPrecision(18, 2);
        builder.Property(e => e.Day20).HasPrecision(18, 2);
        builder.Property(e => e.Day21).HasPrecision(18, 2);
        builder.Property(e => e.Day22).HasPrecision(18, 2);
        builder.Property(e => e.Day23).HasPrecision(18, 2);
        builder.Property(e => e.Day24).HasPrecision(18, 2);
        builder.Property(e => e.Day25).HasPrecision(18, 2);
        builder.Property(e => e.Day26).HasPrecision(18, 2);
        builder.Property(e => e.Day27).HasPrecision(18, 2);
        builder.Property(e => e.Day28).HasPrecision(18, 2);
        builder.Property(e => e.Day29).HasPrecision(18, 2);
        builder.Property(e => e.Day30).HasPrecision(18, 2);
        builder.Property(e => e.Day31).HasPrecision(18, 2);
        builder.Property(e => e.AverageBalance).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.AccountCode, e.PeriodCode }).IsUnique().HasDatabaseName("UK_ACC_RiskCategories_Natural");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
