using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Schedules;

/// <summary>
/// <c>LND_RepaymentInstallments</c> (feature 013; data-model §4.5): hecho <c>bigint</c>; único <c>(ScheduleVersionId,
/// InstallmentNumber)</c> sin filtro; todos los valores de la cuota en pesos.
/// </summary>
public class RepaymentInstallmentConfiguration : IEntityTypeConfiguration<RepaymentInstallment>
{
    public const string Tabla = "LND_RepaymentInstallments";

    public void Configure(EntityTypeBuilder<RepaymentInstallment> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);

        builder.Property(e => e.InstallmentNumber).IsRequired();
        builder.Property(e => e.PeriodStart).IsRequired();
        builder.Property(e => e.DueDate).IsRequired();
        builder.Property(e => e.ExigibleDate).IsRequired();
        builder.Property(e => e.OpeningPrincipal).Monto().IsRequired();
        builder.Property(e => e.Principal).Monto().IsRequired();
        builder.Property(e => e.Interest).Monto().IsRequired();
        builder.Property(e => e.GraceInterest).Monto().IsRequired();
        builder.Property(e => e.Insurance).Monto().IsRequired();
        builder.Property(e => e.OtherCharges).Monto().IsRequired();
        builder.Property(e => e.Total).Monto().IsRequired();
        builder.Property(e => e.ClosingPrincipal).Monto().IsRequired();

        builder.HasOne(e => e.ScheduleVersion).WithMany().HasForeignKey(e => e.ScheduleVersionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ScheduleVersionId, e.InstallmentNumber }).IsUnique()
            .HasDatabaseName("UK_LND_RepaymentInstallments_Version_Number");
    }
}
