using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

public class SalespersonConfiguration : IEntityTypeConfiguration<Salesperson>
{
    public void Configure(EntityTypeBuilder<Salesperson> builder)
    {
        builder.ToTable("INV_Salespeople");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_INV_Salespeople_PublicId");

        // PersonId NOT NULL — every salesperson is a Person. Único entre las filas vivas (feature 012, T427; data-model §11):
        // RolDeVendedor restaura la misma fila, y el filtro deja además que una baja histórica no estorbe.
        builder.Property(e => e.PersonId).IsRequired();
        builder.HasIndex(e => e.PersonId).IsUnique().HasDatabaseName("UK_INV_Salespeople_PersonId").HasFilter("[IsDeleted] = 0");

        builder.HasOne(e => e.Person)
            .WithMany()
            .HasForeignKey(e => e.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
