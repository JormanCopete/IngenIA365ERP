using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// Mapea <see cref="CentralUserLoginAttempt"/> a <c>ADM_CentralUserLoginAttempts</c>.
/// Append-only — sin <c>HasQueryFilter</c> ni RowVersion (no se actualiza después
/// de insertarse). Retención gestionada por job de limpieza separado (1 año).
/// </summary>
public class CentralUserLoginAttemptConfiguration : IEntityTypeConfiguration<CentralUserLoginAttempt>
{
    public void Configure(EntityTypeBuilder<CentralUserLoginAttempt> builder)
    {
        builder.ToTable("ADM_CentralUserLoginAttempts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Result).HasConversion<int>().IsRequired();
        builder.Property(e => e.IpAddress).HasMaxLength(45);
        builder.Property(e => e.UserAgent).HasMaxLength(512);
        builder.Property(e => e.Timestamp);

        // Análisis de fuerza bruta por email (research D-11).
        builder.HasIndex(e => new { e.NormalizedEmail, e.Timestamp })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ADM_LoginAttempts_Email_Timestamp");

        // Análisis forense por IP (no incluido en query filter porque puede ser nulo).
        builder.HasIndex(e => new { e.IpAddress, e.Timestamp })
            .IsDescending(false, true)
            .HasFilter("[IpAddress] IS NOT NULL")
            .HasDatabaseName("IX_ADM_LoginAttempts_Ip_Timestamp");
    }
}
