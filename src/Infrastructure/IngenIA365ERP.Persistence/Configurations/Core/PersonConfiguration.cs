using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("COR_People");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_People_PublicId");

        // Identification
        builder.Property(e => e.LegacyCode).HasMaxLength(20);
        builder.Property(e => e.LastName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(150).IsRequired();
        // Feature 010 (D-06): segundo apellido y otros nombres para PILA y DIAN; nullable, sin migración de datos.
        builder.Property(e => e.SecondLastName).HasMaxLength(150);
        builder.Property(e => e.OtherNames).HasMaxLength(150);
        builder.Property(e => e.TaxId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.TaxIdCheckDigit).HasMaxLength(2);
        builder.Property(e => e.IdIssuedAt).HasMaxLength(40);
        builder.Property(e => e.IdType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.PersonType).HasMaxLength(2);
        builder.Property(e => e.BusinessName).HasMaxLength(150);
        builder.Property(e => e.PreviousCode).HasMaxLength(20);
        builder.Property(e => e.NaturalLegalType).HasDefaultValue((short)0);

        // Contact
        builder.Property(e => e.Address).HasMaxLength(120);
        builder.Property(e => e.Phone1).HasMaxLength(40);
        builder.Property(e => e.Phone2).HasMaxLength(40);
        builder.Property(e => e.Fax).HasMaxLength(30);
        builder.Property(e => e.Mobile).HasMaxLength(30);
        builder.Property(e => e.Email).HasMaxLength(120);
        builder.Property(e => e.MailingAddress).HasMaxLength(120);
        builder.Property(e => e.MailingPreference).HasMaxLength(2);
        builder.Property(e => e.EmailType).HasMaxLength(2);
        builder.Property(e => e.DaneCityCode).HasMaxLength(20);

        // Demographics
        builder.Property(e => e.Gender).HasMaxLength(2);
        builder.Property(e => e.MaritalStatus).HasMaxLength(2);
        builder.Property(e => e.EducationLevel).HasMaxLength(2);
        builder.Property(e => e.SocialStratum).HasMaxLength(4);
        builder.Property(e => e.HousingType).HasMaxLength(2);
        builder.Property(e => e.HasVehicle).HasDefaultValue(false);
        builder.Property(e => e.VehicleType).HasDefaultValue(0);
        builder.Property(e => e.IsHeadOfHousehold).HasDefaultValue(false);
        builder.Property(e => e.WorkShift).HasMaxLength(2);

        // Tax & Regulatory
        builder.Property(e => e.WithholdingExempt).HasDefaultValue(false);
        builder.Property(e => e.IcaWithholdingExempt).HasDefaultValue(false);
        builder.Property(e => e.TaxRegime).HasMaxLength(2);
        builder.Property(e => e.IcaType).HasMaxLength(6);
        builder.Property(e => e.IsLargeContributor).HasDefaultValue(false);
        // Feature 012 (T172, T24): perfil tributario, aditivas con DEFAULT 0; par PlataformaParaInventario.
        builder.Property(e => e.IsVatResponsible).HasDefaultValue(false);
        builder.Property(e => e.IsSelfWithholder).HasDefaultValue(false);
        builder.Property(e => e.IsVatWithholdingAgent).HasDefaultValue(false);
        builder.Property(e => e.IsSimpleTaxRegime).HasDefaultValue(false);
        builder.Property(e => e.IsIncomeTaxFiler).HasDefaultValue(false);
        builder.Property(e => e.IsObligatedToInvoice).HasDefaultValue(false);
        builder.Property(e => e.IcaRate).HasPrecision(10, 5);
        builder.Property(e => e.DataOrigin).HasMaxLength(6);
        builder.Property(e => e.PaymentDays).HasDefaultValue((short)0);
        builder.Property(e => e.HasTaxLien).HasDefaultValue(false);
        builder.Property(e => e.HasSpecialPrice).HasDefaultValue(false);
        builder.Property(e => e.IsEmployerClient).HasDefaultValue(false);
        builder.Property(e => e.SourceWithholding).HasDefaultValue(false);
        builder.Property(e => e.NaturalHasRut).HasDefaultValue(false);
        builder.Property(e => e.CiiuCode).HasMaxLength(20);
        builder.Property(e => e.ThirdPartyType).HasMaxLength(2);

        builder.Property(e => e.WithholdingAux).HasDefaultValue(false);
        builder.Property(e => e.WithholdingAuxAmount).HasPrecision(17, 2);
        builder.Property(e => e.WithholdingAuxPct).HasPrecision(7, 4);
        builder.Property(e => e.WithholdingAuxAccount).HasMaxLength(20);

        // Supplier banking
        builder.Property(e => e.SupplierBankCode).HasMaxLength(20);
        builder.Property(e => e.SupplierBankAccountType).HasMaxLength(2);
        builder.Property(e => e.SupplierBankAccountNumber).HasMaxLength(30);
        builder.Property(e => e.SupplierAdvisorId).HasMaxLength(20);

        // Role flags
        builder.Property(e => e.IsAssociate).HasDefaultValue(false);
        builder.Property(e => e.IsEmployee).HasDefaultValue(false);
        builder.Property(e => e.IsAdvisor).HasDefaultValue(false);
        builder.Property(e => e.IsThirdParty).HasDefaultValue(false);
        builder.Property(e => e.IsCustomer).HasDefaultValue(false);
        builder.Property(e => e.IsSupplier).HasDefaultValue(false);
        builder.Property(e => e.IsSalesperson).HasDefaultValue(false);
        builder.Property(e => e.ReceivesInvoice).HasDefaultValue(false);

        // Status
        builder.Property(e => e.Status).HasMaxLength(2);
        builder.Property(e => e.IsDisabled).HasDefaultValue(false);
        builder.Property(e => e.IsInsolvent).HasDefaultValue(false);
        builder.Property(e => e.IsDeceased).HasDefaultValue(false);

        // Legacy audit
        builder.Property(e => e.LegacyUser).HasMaxLength(20);
        builder.Property(e => e.LegacyUserName).HasMaxLength(80);

        // Unique constraints
        builder.HasIndex(e => e.TaxId).IsUnique().HasDatabaseName("UK_COR_People_TaxId");

        // Search indexes
        builder.HasIndex(e => e.LegacyCode).HasDatabaseName("IX_COR_People_LegacyCode");
        builder.HasIndex(e => e.CityId).HasDatabaseName("IX_COR_People_CityId");
        builder.HasIndex(e => new { e.LastName, e.FirstName }).HasDatabaseName("IX_COR_People_FullName");

        // Relationships
        builder.HasOne(e => e.City).WithMany(c => c.People)
            .HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.MailingCity).WithMany()
            .HasForeignKey(e => e.MailingCityId).OnDelete(DeleteBehavior.Restrict);

        // One-to-one children configured from child side (Cascade)
        builder.HasOne(e => e.Associate).WithOne(a => a.Person)
            .HasForeignKey<Associate>(a => a.PersonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Spouse).WithOne(s => s.Person)
            .HasForeignKey<Spouse>(s => s.PersonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Financial).WithOne(f => f.Person)
            .HasForeignKey<PersonFinancial>(f => f.PersonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.AssociateCategory).WithOne(ac => ac.Person)
            .HasForeignKey<AssociateCategory>(ac => ac.PersonId).OnDelete(DeleteBehavior.Cascade);

        // One-to-many
        builder.HasMany(e => e.Beneficiaries).WithOne(b => b.Person)
            .HasForeignKey(b => b.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.References).WithOne(r => r.Person)
            .HasForeignKey(r => r.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.CommitteeMemberships).WithOne(cm => cm.Person)
            .HasForeignKey(cm => cm.PersonId).OnDelete(DeleteBehavior.Restrict);
        // Notifications quedan asociadas por RecipientUserPublicId (US6) — sin FK a Person.

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
