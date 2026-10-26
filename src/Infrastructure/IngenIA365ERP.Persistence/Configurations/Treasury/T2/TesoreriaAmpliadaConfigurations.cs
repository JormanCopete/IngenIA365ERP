using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.T2;

// Feature 015, T2 (par TesoreriaAmpliada; data-model §13.2; DT §2.3): las tablas TES_ de T2. FK Restrict a la plataforma y entre tablas TES_; ninguna a RCD_, ACC_, INV_
// ni TRS_ (lo de otro módulo se guarda por su PublicId o como texto). Cada fase de T2 suma las suyas y regenera el par.

/// <summary><c>TES_LegacyPendingLoads</c>: por clase y referencia heredada (única entre las vivas). (nuevo)</summary>
public class LegacyPendingLoadConfiguration : IEntityTypeConfiguration<LegacyPendingLoad>
{
    public const string Tabla = "TES_LegacyPendingLoads";

    public void Configure(EntityTypeBuilder<LegacyPendingLoad> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.BeneficiaryPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.LoadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.LegacyReference).HasMaxLength(60).IsRequired();
        builder.Property(e => e.BeneficiaryName).HasMaxLength(200);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.InvoiceNumber).HasMaxLength(40);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => new { e.Kind, e.LegacyReference }).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Kind_Reference");
    }
}
