using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.ElectronicInvoicing;

/// <summary>
/// <c>COR_DianContingencyEvents</c> (feature 012, I4, T698; data-model §18; contracts/dian.md §7): un solo evento abierto
/// por tipo y canal, único <c>(Type, ChannelCode)</c> filtrado <c>[Status] = 1 AND [IsDeleted] = 0</c>. FK <c>Restrict</c>
/// a <c>SEC_Users</c> (quién lo detectó y quién lo cerró). Llega con la migración <c>DocumentosElectronicos</c>.
/// </summary>
public class DianContingencyEventConfiguration : IEntityTypeConfiguration<DianContingencyEvent>
{
    public const string Tabla = "COR_DianContingencyEvents";

    public void Configure(EntityTypeBuilder<DianContingencyEvent> builder)
    {
        builder.ComoEntidadDeFacturacionElectronica(Tabla);

        builder.Property(e => e.Type).HasConversion<int>().IsRequired();
        builder.Property(e => e.ChannelCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.EndedAt);
        builder.Property(e => e.DetectedByKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.DetectedByName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.DeadlineHoursApplied).IsRequired();
        builder.Property(e => e.LegalSource).HasMaxLength(200).IsRequired();
        builder.Property(e => e.DeadlineAt);
        builder.Property(e => e.DeclaredToDianAt);
        builder.Property(e => e.DeclaredToDianReference).HasMaxLength(60);
        builder.Property(e => e.ClosedByKind).HasConversion<int?>();
        builder.Property(e => e.ClosedByName).HasMaxLength(150);
        builder.Property(e => e.CloseReason).HasMaxLength(300);

        builder.Ignore(e => e.EstaAbierto);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DetectedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Type, e.ChannelCode })
            .IsUnique()
            .HasDatabaseName("UK_COR_DianContingencyEvents_Type_Channel_Open")
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");
    }
}
