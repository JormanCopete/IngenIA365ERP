using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Operations;

/// <summary>
/// <c>LND_DisbursementFileLines</c> (feature 013; data-model §7.7): hecho hijo del archivo; número de línea único por archivo y una
/// orden a lo sumo una vez por archivo.
/// </summary>
public class DisbursementFileLineConfiguration : IEntityTypeConfiguration<DisbursementFileLine>
{
    public const string Tabla = "LND_DisbursementFileLines";

    public void Configure(EntityTypeBuilder<DisbursementFileLine> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.LineNumber).IsRequired();
        builder.Property(e => e.AccountType).IsRequired();
        builder.Property(e => e.AccountNumber).HasMaxLength(25).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.RecordText).HasMaxLength(600).IsRequired();

        builder.HasOne<DisbursementFile>().WithMany().HasForeignKey(e => e.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DisbursementOrder>().WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.BeneficiaryPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.DestinationBankId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.FileId, e.LineNumber }).IsUnique().HasDatabaseName("UK_LND_DisbursementFileLines_File_Line");
        builder.HasIndex(e => new { e.FileId, e.OrderId }).IsUnique().HasDatabaseName("UK_LND_DisbursementFileLines_File_Order");
    }
}
