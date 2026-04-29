using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("COR_SystemSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_SystemSettings_PublicId");

        builder.Property(e => e.SettingKey).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ValueType).HasMaxLength(20).IsRequired().HasDefaultValue("String");
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.ModulePrefix).HasMaxLength(10);

        // Unique: SettingKey
        builder.HasIndex(e => e.SettingKey).IsUnique().HasDatabaseName("UK_COR_SystemSettings_Key");

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
