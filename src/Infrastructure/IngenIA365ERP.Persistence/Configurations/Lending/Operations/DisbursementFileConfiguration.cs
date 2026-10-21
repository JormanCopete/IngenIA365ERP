using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Operations;

/// <summary>
/// <c>LND_DisbursementFiles</c> (feature 013; data-model §7.6): hecho con anulación de escritura única; número único e índice por día
/// de pago y secuencia. La cuenta de origen y el adjunto son datos sin FK.
/// </summary>
public class DisbursementFileConfiguration : IEntityTypeConfiguration<DisbursementFile>
{
    public const string Tabla = "LND_DisbursementFiles";

    public void Configure(EntityTypeBuilder<DisbursementFile> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.FormatCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PaymentDate).IsRequired();
        builder.Property(e => e.DailySequence).IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(60);
        builder.Property(e => e.SourceAccountPublicId).IsRequired();
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.LineCount).IsRequired();
        builder.Property(e => e.TotalAmount).Monto().IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FileSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.FileAttachmentPublicId).IsRequired();
        builder.Property(e => e.GeneratedAt).IsRequired();
        builder.Property(e => e.VoidReason).HasMaxLength(500);

        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankFileFormat>().WithMany().HasForeignKey(e => e.FormatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.GeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_DisbursementFiles_Number");
        builder.HasIndex(e => new { e.PaymentDate, e.DailySequence }).HasDatabaseName("IX_LND_DisbursementFiles_PaymentDate_Sequence");
    }
}
