using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

/// <summary>
/// <c>COR_PersonBankAccounts</c> (feature 015, E-06, T0028; data-model §16.1): FK <c>Restrict</c> a <c>COR_People</c> y <c>COR_Banks</c>, el índice
/// <c>(BankId, AccountNumber)</c> con que <c>ICuentasBancariasDePersona</c> detecta la cuenta compartida entre beneficiarios o con un usuario
/// interno, y <c>ReplacesId</c> a la fila que cada cambio reemplaza. Los estados y orígenes se guardan como entero.
/// </summary>
public class PersonBankAccountConfiguration : IEntityTypeConfiguration<PersonBankAccount>
{
    public const string Tabla = "COR_PersonBankAccounts";

    public void Configure(EntityTypeBuilder<PersonBankAccount> builder)
    {
        builder.ToTable(Tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_PublicId");
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.Property(e => e.AccountType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.AccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.HolderName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.HolderTaxId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Origin).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.RequestedByName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DecidedByName).HasMaxLength(100);
        builder.Property(e => e.Reason).HasMaxLength(500);

        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Bank).WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Replaces).WithMany().HasForeignKey(e => e.ReplacesId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.BankId, e.AccountNumber }).HasDatabaseName($"IX_{Tabla}_BankId_AccountNumber");
        builder.HasIndex(e => new { e.PersonId, e.Status }).HasDatabaseName($"IX_{Tabla}_PersonId_Status");
        builder.HasIndex(e => e.ReplacesId).HasDatabaseName($"IX_{Tabla}_ReplacesId");

        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
