using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Transport;

// Feature 020 (R3, R6; data-model §2): vehículos, su historia, sus equipos y los catálogos de Transporte. Los índices únicos de
// placa, número interno y radio-móvil son «entre vivos»: sin borrar y no retirados (Status 3), así un retiro libera los tres (FR-010).
// Los filtros se escriben con corchetes y 0/1; ProviderModelConventions los traduce a PostgreSQL.

/// <summary>Lo común de toda tabla de Transporte: clave, PublicId único, auditoría y filtro de borrado lógico.</summary>
internal static class EntidadDeTransporte
{
    public static EntityTypeBuilder<T> ComoEntidadDeTransporte<T>(this EntityTypeBuilder<T> builder, string tabla) where T : AuditableEntity
    {
        builder.ToTable(tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{tabla}_PublicId");
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(e => !e.IsDeleted);
        return builder;
    }
}

public class TransportCatalogItemConfiguration : IEntityTypeConfiguration<TransportCatalogItem>
{
    public void Configure(EntityTypeBuilder<TransportCatalogItem> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_CatalogItems");
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(e => new { e.Kind, e.Code }).IsUnique().HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UK_TRN_CatalogItems_Kind_Code");
    }
}

public class TransportDocumentTypeConfiguration : IEntityTypeConfiguration<TransportDocumentType>
{
    public void Configure(EntityTypeBuilder<TransportDocumentType> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_DocumentTypes");
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UK_TRN_DocumentTypes_Code");
    }
}

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    private const string Vivos = "[IsDeleted] = 0 AND [Status] <> 3";

    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_Vehicles");

        builder.Property(e => e.Plate).HasMaxLength(10).IsRequired();
        builder.Property(e => e.InternalNumber).HasMaxLength(10).IsRequired();
        builder.Property(e => e.RadioCode).HasMaxLength(20);
        builder.Property(e => e.Line).HasMaxLength(60);
        builder.Property(e => e.Color).HasMaxLength(40);
        builder.Property(e => e.EngineNumber).HasMaxLength(40);
        builder.Property(e => e.ChassisNumber).HasMaxLength(40);
        builder.Property(e => e.Vin).HasMaxLength(40);
        builder.Property(e => e.TransitLicenseNumber).HasMaxLength(30);
        builder.Property(e => e.AffiliationContractNumber).HasMaxLength(30);
        builder.Property(e => e.OperationCardNumber).HasMaxLength(30);
        builder.Property(e => e.RetirementReason).HasMaxLength(500);
        builder.Property(e => e.MinDailyRolling).HasPrecision(18, 2);
        builder.Property(e => e.MinMonthlyRolling).HasPrecision(18, 2);

        builder.HasIndex(e => e.Plate).IsUnique().HasFilter(Vivos).HasDatabaseName("UK_TRN_Vehicles_Plate");
        builder.HasIndex(e => e.InternalNumber).IsUnique().HasFilter(Vivos).HasDatabaseName("UK_TRN_Vehicles_InternalNumber");
        builder.HasIndex(e => e.RadioCode).IsUnique().HasFilter(Vivos + " AND [RadioCode] IS NOT NULL").HasDatabaseName("UK_TRN_Vehicles_RadioCode");
        builder.HasIndex(e => e.OwnerPersonId).HasDatabaseName("IX_TRN_Vehicles_OwnerPersonId");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_TRN_Vehicles_Status");

        builder.HasOne(e => e.OwnerPerson).WithMany().HasForeignKey(e => e.OwnerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Brand).WithMany().HasForeignKey(e => e.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.VehicleClass).WithMany().HasForeignKey(e => e.VehicleClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.BodyType).WithMany().HasForeignKey(e => e.BodyTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Fuel).WithMany().HasForeignKey(e => e.FuelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TransitAuthority).WithMany().HasForeignKey(e => e.TransitAuthorityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ServiceClass).WithMany().HasForeignKey(e => e.ServiceClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Modality).WithMany().HasForeignKey(e => e.ModalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.RollingGroup).WithMany().HasForeignKey(e => e.RollingGroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class VehicleEventConfiguration : IEntityTypeConfiguration<VehicleEvent>
{
    public void Configure(EntityTypeBuilder<VehicleEvent> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_VehicleEvents");
        builder.Property(e => e.FromValue).HasMaxLength(60);
        builder.Property(e => e.ToValue).HasMaxLength(60);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.ChangedByName).HasMaxLength(150);
        builder.HasOne(e => e.Vehicle).WithMany(v => v.Events).HasForeignKey(e => e.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.VehicleId, e.Kind, e.EffectiveDate }).HasDatabaseName("IX_TRN_VehicleEvents_Vehicle_Kind_Date");
    }
}

public class VehicleEquipmentConfiguration : IEntityTypeConfiguration<VehicleEquipment>
{
    public void Configure(EntityTypeBuilder<VehicleEquipment> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_VehicleEquipment");
        builder.Property(e => e.Code).HasMaxLength(40);
        builder.HasOne(e => e.Vehicle).WithMany(v => v.Equipment).HasForeignKey(e => e.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.EquipmentType).WithMany().HasForeignKey(e => e.EquipmentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.VehicleId, e.EquipmentTypeId }).IsUnique().HasFilter("[IsDeleted] = 0 AND [Status] = 1")
            .HasDatabaseName("UK_TRN_VehicleEquipment_Vehicle_Type");
    }
}

public class VehicleDocumentConfiguration : IEntityTypeConfiguration<VehicleDocument>
{
    public void Configure(EntityTypeBuilder<VehicleDocument> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_VehicleDocuments");
        builder.Property(e => e.Number).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Issuer).HasMaxLength(120);
        builder.HasOne(e => e.Vehicle).WithMany().HasForeignKey(e => e.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DocumentType).WithMany().HasForeignKey(e => e.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.VehicleId, e.DocumentTypeId }).IsUnique().HasFilter("[IsDeleted] = 0 AND [SupersededAt] IS NULL")
            .HasDatabaseName("UK_TRN_VehicleDocuments_Vehicle_Type_Vigente");
        builder.HasIndex(e => e.ExpiresOn).HasDatabaseName("IX_TRN_VehicleDocuments_ExpiresOn");
    }
}

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_Drivers");
        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.PersonId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UK_TRN_Drivers_PersonId");
    }
}

public class DriverAssignmentConfiguration : IEntityTypeConfiguration<DriverAssignment>
{
    public void Configure(EntityTypeBuilder<DriverAssignment> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_DriverAssignments");
        builder.HasOne(e => e.Driver).WithMany(d => d.Assignments).HasForeignKey(e => e.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Vehicle).WithMany().HasForeignKey(e => e.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.VehicleId, e.DriverId }).HasDatabaseName("IX_TRN_DriverAssignments_Vehicle_Driver");
    }
}
