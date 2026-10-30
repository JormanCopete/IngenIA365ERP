using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

// Feature 015, US10 (T0137; E-04; R-22; data-model §17.3): el lado contable de la integración con Tesorería, enmienda E-04 de la 009. Llega con el par
// IntegracionContableDeRecaudoYTesoreria (las matrices y los recibos de contabilización se suman en la fase 16). Sin FK a tablas TES_: la cuenta propia va por su
// PublicId (TesoreriaNoConoceContabilidad en las dos direcciones).

/// <summary>
/// <c>ACC_OwnBankAccountLinks</c>: el vínculo vigente es único en los dos sentidos —una cuenta propia con una cuenta contable y una cuenta contable con una cuenta
/// propia— (dos índices únicos filtrados a vigentes, <c>Accounting.OwnBankAccount.AlreadyLinked</c>) y la FK <c>Restrict</c> a <c>ACC_ChartOfAccounts</c>. (nuevo)
/// </summary>
public class OwnBankAccountLinkConfiguration : IEntityTypeConfiguration<OwnBankAccountLink>
{
    public const string Tabla = "ACC_OwnBankAccountLinks";

    public void Configure(EntityTypeBuilder<OwnBankAccountLink> builder)
    {
        builder.ToTable(Tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_PublicId");
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.HasOne<ChartOfAccount>().WithMany().HasForeignKey(e => e.ChartOfAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.OwnBankAccountPublicId).IsUnique().HasFilter("[ValidTo] IS NULL AND [IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_OwnBankAccountPublicId_Vigente");
        builder.HasIndex(e => e.ChartOfAccountId).IsUnique().HasFilter("[ValidTo] IS NULL AND [IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_ChartOfAccountId_Vigente");
    }
}
