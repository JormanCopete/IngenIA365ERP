using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

public class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> builder)
    {
        builder.ToTable("ADM_TenantSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.SettingKey }).IsUnique();

        builder.Property(e => e.SettingKey).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ValueType).HasMaxLength(30).HasDefaultValue("String");
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.ModulePrefix).HasMaxLength(5);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
