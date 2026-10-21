using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary>
/// <c>LND_BureauQueries</c> (feature 013; data-model §2.7): hecho de consultas a centrales; la autorización y el informe van por
/// <c>PublicId</c>, sin FK.
/// </summary>
public class BureauQueryConfiguration : IEntityTypeConfiguration<BureauQuery>
{
    public const string Tabla = "LND_BureauQueries";

    public void Configure(EntityTypeBuilder<BureauQuery> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Purpose).HasConversion<int>().IsRequired();
        builder.Property(e => e.BureauCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ChannelCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.AuthorizationConsentPublicId).IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>().IsRequired();
        builder.Property(e => e.Score).Puntaje();
        builder.Property(e => e.Summary);
        builder.Property(e => e.ReportAttachmentPublicId);
        builder.Property(e => e.IsSimulated).IsRequired();
        builder.Property(e => e.QueriedAt).IsRequired();
        builder.Property(e => e.ValidUntil).IsRequired();

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.QueriedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
