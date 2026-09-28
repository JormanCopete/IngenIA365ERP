using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.ElectronicInvoicing;

/// <summary>
/// <c>COR_ElectronicDocuments</c> (feature 012, I4, T698; data-model §18): uno por número fiscal. Único
/// <c>(Environment, Prefix, Consecutive)</c> <b>sin filtro</b> (un número nunca se duplica, ni contra filas dadas de baja:
/// estas filas no se borran); único <c>(SourceModule, SourceDocumentPublicId, Kind)</c>; <c>(Status, NextAttemptAt)</c>
/// para el procesador; <c>(UniqueCode)</c> filtrado no nulo; <c>(IssueDate)</c> y <c>(ContingencyEventId)</c> para la
/// bandeja. FK <c>Restrict</c> a la resolución, a la configuración sellada, al evento de contingencia, a la versión vigente
/// (la dependencia circular documento ↔ versión la resuelve EF insertando y actualizando <c>CurrentVersionId</c>), a sí
/// misma (<c>CorrectsDocumentId</c>, <c>WaitsForDocumentId</c>) y a <c>SEC_Users</c> (caso c). Llega con la migración
/// <c>DocumentosElectronicos</c>.
/// </summary>
public class ElectronicDocumentConfiguration : IEntityTypeConfiguration<ElectronicDocument>
{
    public const string Tabla = "COR_ElectronicDocuments";

    public void Configure(EntityTypeBuilder<ElectronicDocument> builder)
    {
        builder.ComoEntidadDeFacturacionElectronica(Tabla);

        builder.Property(e => e.SourceModule).HasMaxLength(3).IsRequired();
        builder.Property(e => e.SourceDocumentPublicId).IsRequired();
        builder.Property(e => e.SourceDocumentTypeCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.DianDocumentTypeCode).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Prefix).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Consecutive).IsRequired();
        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Environment).HasConversion<int>().IsRequired();
        builder.Property(e => e.EmissionSettingId).IsRequired();
        builder.Property(e => e.Mode).HasConversion<int>().IsRequired();
        builder.Property(e => e.ChannelCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SoftwareId).HasMaxLength(36);
        builder.Property(e => e.IssuedAt).IsRequired();
        builder.Property(e => e.IssueDate).IsRequired();
        builder.Property(e => e.CounterpartyTaxId).HasMaxLength(20);
        builder.Property(e => e.CounterpartyName).HasMaxLength(200);
        builder.Property(e => e.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.UniqueCode).HasMaxLength(96);
        builder.Property(e => e.UniqueCodeKind).HasConversion<int?>();
        builder.Property(e => e.QrContent).HasMaxLength(1000);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ContingencyType).HasConversion<int?>();
        builder.Property(e => e.SentAt);
        builder.Property(e => e.ValidatedAt);
        builder.Property(e => e.DeliveredAt);
        builder.Property(e => e.EmailDeliveryBy).HasConversion<int>().IsRequired();
        builder.Property(e => e.EmailSentAt);
        builder.Property(e => e.RejectedBy).HasConversion<int?>();
        builder.Property(e => e.RejectionReason).HasMaxLength(500);
        builder.Property(e => e.AttemptCount).IsRequired();
        builder.Property(e => e.NextAttemptAt);
        builder.Property(e => e.LeaseUntil);
        builder.Property(e => e.LeaseOwner).HasMaxLength(100);
        builder.Property(e => e.TransmissionDeadline);
        builder.Property(e => e.LastOutcome).HasConversion<int?>();
        builder.Property(e => e.LastMessagesJson);

        builder.Ignore(e => e.EsFinal);

        builder.HasOne(e => e.Resolution).WithMany().HasForeignKey(e => e.ResolutionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.EmissionSetting).WithMany().HasForeignKey(e => e.EmissionSettingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ContingencyEvent).WithMany().HasForeignKey(e => e.ContingencyEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CorrectsDocument).WithMany().HasForeignKey(e => e.CorrectsDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.WaitsForDocument).WithMany().HasForeignKey(e => e.WaitsForDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicDocumentVersion>().WithMany().HasForeignKey(e => e.CurrentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Versions).WithOne(v => v.ElectronicDocument).HasForeignKey(v => v.ElectronicDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Transmissions).WithOne(t => t.ElectronicDocument).HasForeignKey(t => t.ElectronicDocumentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Environment, e.Prefix, e.Consecutive })
            .IsUnique()
            .HasDatabaseName("UK_COR_ElectronicDocuments_Environment_Prefix_Consecutive");
        builder.HasIndex(e => new { e.SourceModule, e.SourceDocumentPublicId, e.Kind })
            .IsUnique()
            .HasDatabaseName("UK_COR_ElectronicDocuments_Source_Kind");
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt })
            .HasDatabaseName("IX_COR_ElectronicDocuments_Status_NextAttemptAt");
        builder.HasIndex(e => e.UniqueCode)
            .HasDatabaseName("IX_COR_ElectronicDocuments_UniqueCode")
            .HasFilter("[UniqueCode] IS NOT NULL");
        builder.HasIndex(e => e.IssueDate).HasDatabaseName("IX_COR_ElectronicDocuments_IssueDate");
        builder.HasIndex(e => e.ContingencyEventId).HasDatabaseName("IX_COR_ElectronicDocuments_ContingencyEventId");
    }
}
