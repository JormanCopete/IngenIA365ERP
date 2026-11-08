using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Persistence.Configurations.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Channel;

// Feature 021 (data-model.md §A): las tablas CDG_ del canal digital. Mismo armazón que las de Recaudo (PublicId único,
// RowVersion, borrado lógico con filtro); FK Restrict hacia la plataforma y hacia Recaudo.

/// <summary><c>CDG_ChannelSettings</c>: una fila por versión; la versión es única.</summary>
public class ChannelSettingsConfiguration : IEntityTypeConfiguration<ChannelSettings>
{
    public const string Tabla = "CDG_ChannelSettings";

    public void Configure(EntityTypeBuilder<ChannelSettings> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.Property(e => e.ChannelCode).HasMaxLength(3);
        builder.Property(e => e.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(e => e.PrimaryColor).HasMaxLength(7);
        builder.Property(e => e.SecondaryColor).HasMaxLength(7);
        builder.Property(e => e.SupportPhone).HasMaxLength(40);
        builder.Property(e => e.SupportEmail).HasMaxLength(150);
        builder.Property(e => e.SupportHours).HasMaxLength(120);
        builder.Property(e => e.EnablementPolicy).HasConversion<int>();
        builder.Property(e => e.SurplusPolicy).HasConversion<int>();
        builder.HasIndex(e => e.Version).IsUnique().HasDatabaseName($"UK_{Tabla}_Version");
    }
}

/// <summary><c>CDG_LegalTexts</c>: por tipo y versión.</summary>
public class ChannelLegalTextConfiguration : IEntityTypeConfiguration<ChannelLegalText>
{
    public const string Tabla = "CDG_LegalTexts";

    public void Configure(EntityTypeBuilder<ChannelLegalText> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Content).IsRequired();
        builder.HasIndex(e => new { e.Kind, e.Version }).IsUnique().HasDatabaseName($"UK_{Tabla}_Kind_Version");
    }
}

/// <summary><c>CDG_PaymentDestinations</c>: FK al convenio de recaudo; el sitio es único.</summary>
public class ChannelPaymentDestinationConfiguration : IEntityTypeConfiguration<ChannelPaymentDestination>
{
    public const string Tabla = "CDG_PaymentDestinations";

    public void Configure(EntityTypeBuilder<ChannelPaymentDestination> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CollectionAgreement>().WithMany().HasForeignKey(e => e.CollectionAgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Gateway).HasConversion<int>();
        builder.Property(e => e.CredentialsStatus).HasConversion<int>();
        builder.Property(e => e.LoginLast4).HasMaxLength(4);
        builder.HasIndex(e => e.SitePublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_SitePublicId");
    }
}

/// <summary><c>CDG_PublishedConcepts</c>: único por tipo y línea entre vivos.</summary>
public class ChannelPublishedConceptConfiguration : IEntityTypeConfiguration<ChannelPublishedConcept>
{
    public const string Tabla = "CDG_PublishedConcepts";

    public void Configure(EntityTypeBuilder<ChannelPublishedConcept> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<ChannelPaymentDestination>().WithMany().HasForeignKey(e => e.DestinationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.LineCode).HasMaxLength(20);
        builder.Property(e => e.PartialMinimum).HasPrecision(18, 2);
        builder.Property(e => e.DefaultPrepaymentChoice).HasConversion<int?>();
        builder.HasIndex(e => new { e.Kind, e.LineCode })
            .IsUnique()
            .HasFilter(FiltroDeVivos)
            .HasDatabaseName($"UK_{Tabla}_Kind_Line");
    }

    /// <summary>El filtro «no borrado», con la sintaxis que usa todo el proyecto (la migración de PostgreSQL lo traduce).</summary>
    internal const string FiltroDeVivos = "[IsDeleted] = 0";
}

/// <summary><c>CDG_ChannelEnablements</c>: una habilitación viva por persona; el cursor se consulta por orden.</summary>
public class ChannelEnablementConfiguration : IEntityTypeConfiguration<ChannelEnablement>
{
    public const string Tabla = "CDG_ChannelEnablements";

    public void Configure(EntityTypeBuilder<ChannelEnablement> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Source).HasConversion<int>();
        builder.Property(e => e.DisabledReason).HasMaxLength(300);
        builder.HasIndex(e => e.PersonId)
            .IsUnique()
            .HasFilter(ChannelPublishedConceptConfiguration.FiltroDeVivos)
            .HasDatabaseName($"UK_{Tabla}_Person");
        builder.HasIndex(e => e.ChangeSequence).HasDatabaseName($"IX_{Tabla}_ChangeSequence");
    }
}

/// <summary><c>CDG_ContactUpdates</c>: por persona; idempotente por persona, campo y fecha.</summary>
public class ChannelContactUpdateConfiguration : IEntityTypeConfiguration<ChannelContactUpdate>
{
    public const string Tabla = "CDG_ContactUpdates";

    public void Configure(EntityTypeBuilder<ChannelContactUpdate> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Field).HasConversion<int>();
        builder.Property(e => e.NewValue).HasMaxLength(150).IsRequired();
        builder.Property(e => e.AcknowledgedBy).HasMaxLength(100);
        builder.HasIndex(e => new { e.PersonId, e.Field, e.ReportedAt }).IsUnique().HasDatabaseName($"UK_{Tabla}_Person_Field_Reported");
    }
}

/// <summary><c>CDG_PaymentNotices</c>: la referencia es única (idempotencia del aviso, FR-044).</summary>
public class ChannelPaymentNoticeConfiguration : IEntityTypeConfiguration<ChannelPaymentNotice>
{
    public const string Tabla = "CDG_PaymentNotices";

    public void Configure(EntityTypeBuilder<ChannelPaymentNotice> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChannelPaymentDestination>().WithMany().HasForeignKey(e => e.DestinationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReversalReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Items).WithOne().HasForeignKey(i => i.PaymentNoticeId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Reference).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Total).HasPrecision(18, 2);
        builder.Property(e => e.Authorization).HasMaxLength(60);
        builder.Property(e => e.GatewayReceipt).HasMaxLength(60);
        builder.Property(e => e.Franchise).HasMaxLength(30);
        builder.Property(e => e.PaymentMethodName).HasMaxLength(80);
        builder.Property(e => e.IssuerName).HasMaxLength(120);
        builder.Property(e => e.PayerIp).HasMaxLength(46);
        builder.Property(e => e.PayerUserAgent).HasMaxLength(255);
        builder.Property(e => e.Outcome).HasConversion<int>();
        builder.Property(e => e.OutcomeReason).HasMaxLength(300);
        builder.Property(e => e.ReversalReason).HasMaxLength(300);
        builder.HasIndex(e => e.Reference).IsUnique().HasDatabaseName($"UK_{Tabla}_Reference");
        builder.HasIndex(e => new { e.PersonId, e.ApprovedAt }).HasDatabaseName($"IX_{Tabla}_Person_Approved");
    }
}

/// <summary><c>CDG_PaymentNoticeItems</c>: los conceptos de cada aviso.</summary>
public class ChannelPaymentNoticeItemConfiguration : IEntityTypeConfiguration<ChannelPaymentNoticeItem>
{
    public const string Tabla = "CDG_PaymentNoticeItems";

    public void Configure(EntityTypeBuilder<ChannelPaymentNoticeItem> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.Property(e => e.ConceptKind).HasConversion<int>();
        builder.Property(e => e.Modality).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.PrepaymentChoice).HasConversion<int?>();
        builder.HasIndex(e => e.PaymentNoticeId).HasDatabaseName($"IX_{Tabla}_Notice");
    }
}
