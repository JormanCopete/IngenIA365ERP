using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Compliance.HabeasData.Authorizations;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Application.Compliance.HabeasData.PublishPolicyVersion;
using IngenIA365ERP.Application.Tests.Core.People;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IngenIA365ERP.Application.Tests.Compliance;

/// <summary>
/// T0033 (feature 013; T69 a, S10; data-model parte 3 §4.10; contracts/interfaces.md §10.1; quickstart §1.5 «Habeas data»): la autorización
/// de tratamiento de datos por finalidad. <c>VigenteAsync(persona, finalidad, canal?)</c> responde con <c>Purpose</c> y <c>HasEvidence</c>;
/// el método de la 012 (tratamiento general) responde igual que antes; <c>AgregarAsync</c> no guarda; registrar sin política vigente de
/// la finalidad es <c>Compliance.HabeasData.NoCurrentPolicy</c>; hay una versión vigente por finalidad (únicos rehechos con <c>Purpose</c>,
/// C68); y una autorización migrada sin evidencia queda <c>HasEvidence = false</c> (FR-006).
/// </summary>
public class AutorizacionDeDatosPorFinalidadTests
{
    private static readonly DateTime Ahora = PersonasTestData.Ahora;

    private static HabeasDataPolicyVersion Politica(PersonasTestData d, DataAuthorizationPurpose finalidad, int version = 1, bool vigente = true)
    {
        var p = new HabeasDataPolicyVersion
        {
            TenantId = 1, VersionNumber = version, Title = $"{finalidad} v{version}", ContentMarkdown = "Texto de la política",
            Sha256Hex = new string('a', 64), EffectiveFrom = Ahora.AddMonths(-version), EffectiveTo = vigente ? null : Ahora.AddDays(-1),
            PublishedBy = "admin@demo", CreatedBy = "admin@demo", Purpose = finalidad,
        };
        d.Db.HabeasDataPolicyVersions.Add(p);
        d.Db.SaveChanges();
        return p;
    }

    private static Attachment Evidencia(PersonasTestData d, Person titular, string tipo = AdjuntosDeModulo.AutorizacionDeDatos,
        EstadoDeAdjunto estado = EstadoDeAdjunto.Available)
    {
        var a = new Attachment
        {
            TenantId = 1, OwnerEntityType = tipo, OwnerEntityPublicId = titular.PublicId, FileName = "autorizacion.pdf",
            ContentType = "application/pdf", SizeBytes = 10, Sha256Hex = new string('b', 64), StoragePath = "x", Status = estado,
        };
        d.Db.Attachments.Add(a);
        d.Db.SaveChanges();
        return a;
    }

    private static RecordDataAuthorizationCommandHandler Registrar(PersonasTestData d) => new(d.Db, d.Autorizacion, d.Clock);

    [Fact]
    public async Task Vigente_por_finalidad_responde_con_Purpose_y_HasEvidence()
    {
        var d = new PersonasTestData();
        var titular = d.Persona();
        Politica(d, DataAuthorizationPurpose.CreditBureauQuery);
        var evidencia = Evidencia(d, titular);

        var r = await Registrar(d).Handle(new RecordDataAuthorizationCommand(titular.PublicId, DataAuthorizationPurpose.CreditBureauQuery, null, null,
            evidencia.PublicId, DataAuthorizationOrigin.CreditApplication, Ahora.AddHours(-1)), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var vigente = await d.Autorizacion.VigenteAsync(titular.PublicId, DataAuthorizationPurpose.CreditBureauQuery, null, CancellationToken.None);
        vigente.Should().NotBeNull();
        vigente!.Autoriza.Should().BeTrue();
        vigente.Purpose.Should().Be(DataAuthorizationPurpose.CreditBureauQuery);
        vigente.HasEvidence.Should().BeTrue();
        vigente.Origin.Should().Be(DataAuthorizationOrigin.CreditApplication);
        (await d.Autorizacion.VigenteAsync(titular.PublicId, DataAuthorizationPurpose.CreditBureauReport, null, CancellationToken.None))
            .Should().BeNull("cada finalidad se autoriza por separado");
    }

    [Fact]
    public async Task El_metodo_de_la_012_es_el_tratamiento_general_y_no_lo_mueven_las_otras_finalidades()
    {
        var d = new PersonasTestData();
        var titular = d.Persona();
        Politica(d, DataAuthorizationPurpose.GeneralTreatment);
        Politica(d, DataAuthorizationPurpose.ContactChannel);
        (await d.Autorizacion.AgregarAsync(new AutorizacionAAgregar(titular.PublicId, DataAuthorizationPurpose.GeneralTreatment,
            DecisionDeAutorizacion.Accepted, null, null, null, DataAuthorizationOrigin.PersonRegistration, Ahora.AddDays(-2)), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        (await d.Autorizacion.AgregarAsync(new AutorizacionAAgregar(titular.PublicId, DataAuthorizationPurpose.ContactChannel,
            DecisionDeAutorizacion.Declined, null, DataAuthorizationChannel.Sms, null, DataAuthorizationOrigin.CreditApplication, Ahora.AddDays(-1)), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        await d.Db.SaveChangesAsync();

        var general = await d.Autorizacion.VigenteAsync(titular.PublicId, CancellationToken.None);

        general!.Decision.Should().Be(AccionesDeConsentimiento.Accepted, "el «no» posterior al SMS no es el tratamiento general");
        general.Autoriza.Should().BeTrue();
        general.Purpose.Should().Be(DataAuthorizationPurpose.GeneralTreatment);
    }

    [Fact]
    public async Task El_canal_de_contacto_se_autoriza_y_se_consulta_por_canal()
    {
        var d = new PersonasTestData();
        var titular = d.Persona();
        Politica(d, DataAuthorizationPurpose.ContactChannel);
        await d.Autorizacion.AgregarAsync(new AutorizacionAAgregar(titular.PublicId, DataAuthorizationPurpose.ContactChannel,
            DecisionDeAutorizacion.Accepted, null, DataAuthorizationChannel.Sms, Evidencia(d, titular).PublicId, DataAuthorizationOrigin.CreditApplication,
            Ahora.AddDays(-1)), CancellationToken.None);
        await d.Db.SaveChangesAsync();

        (await d.Autorizacion.VigenteAsync(titular.PublicId, DataAuthorizationPurpose.ContactChannel, "Sms", CancellationToken.None))!
            .ContactChannel.Should().Be(DataAuthorizationChannel.Sms);
        (await d.Autorizacion.VigenteAsync(titular.PublicId, DataAuthorizationPurpose.ContactChannel, "WhatsApp", CancellationToken.None))
            .Should().BeNull();
        (await d.Autorizacion.AgregarAsync(new AutorizacionAAgregar(titular.PublicId, DataAuthorizationPurpose.ContactChannel,
            DecisionDeAutorizacion.Accepted, null, null, null, DataAuthorizationOrigin.CreditApplication, Ahora), CancellationToken.None))
            .IsFailure.Should().BeTrue("ContactChannel sin canal no se registra");
    }

    [Fact]
    public async Task Agregar_no_guarda()
    {
        var d = new PersonasTestData();
        var titular = d.Persona();
        Politica(d, DataAuthorizationPurpose.CreditProcessing);

        var r = await d.Autorizacion.AgregarAsync(new AutorizacionAAgregar(titular.PublicId, DataAuthorizationPurpose.CreditProcessing,
            DecisionDeAutorizacion.Accepted, null, null, null, DataAuthorizationOrigin.CreditApplication, Ahora), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        d.Db.ChangeTracker.Entries<HabeasDataConsent>().Should().ContainSingle(e => e.State == EntityState.Added);
        (await d.Db.HabeasDataConsents.AsNoTracking().CountAsync()).Should().Be(0, "la guarda quien llama, en su transacción");
    }

    [Fact]
    public async Task Sin_version_vigente_de_la_finalidad_registrar_es_NoCurrentPolicy()
    {
        var d = new PersonasTestData();
        var titular = d.Persona();
        Politica(d, DataAuthorizationPurpose.GeneralTreatment);
        Politica(d, DataAuthorizationPurpose.CreditBureauReport, vigente: false);

        var r = await Registrar(d).Handle(new RecordDataAuthorizationCommand(titular.PublicId, DataAuthorizationPurpose.CreditBureauReport, null, null,
            Evidencia(d, titular).PublicId, DataAuthorizationOrigin.CreditApplication, Ahora), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(HabeasDataErrorCodes.NoCurrentPolicy);
        (await d.Db.HabeasDataConsents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_evidencia_es_un_adjunto_DataAuthorization_del_titular_y_solo_la_migrada_puede_ir_sin_ella()
    {
        var d = new PersonasTestData();
        var titular = d.Persona();
        var otro = d.Persona("900", "Otra", "Persona");
        Politica(d, DataAuthorizationPurpose.CreditProcessing);

        var sinEvidencia = await Registrar(d).Handle(new RecordDataAuthorizationCommand(titular.PublicId, DataAuthorizationPurpose.CreditProcessing,
            null, null, null, DataAuthorizationOrigin.CreditApplication, Ahora), CancellationToken.None);
        var ajena = await Registrar(d).Handle(new RecordDataAuthorizationCommand(titular.PublicId, DataAuthorizationPurpose.CreditProcessing,
            null, null, Evidencia(d, otro).PublicId, DataAuthorizationOrigin.CreditApplication, Ahora), CancellationToken.None);
        var deOtroTipo = await Registrar(d).Handle(new RecordDataAuthorizationCommand(titular.PublicId, DataAuthorizationPurpose.CreditProcessing,
            null, null, Evidencia(d, titular, "AccountingDocument").PublicId, DataAuthorizationOrigin.CreditApplication, Ahora), CancellationToken.None);
        var migrada = await Registrar(d).Handle(new RecordDataAuthorizationCommand(titular.PublicId, DataAuthorizationPurpose.CreditProcessing,
            null, null, null, DataAuthorizationOrigin.Migration, Ahora.AddYears(-3)), CancellationToken.None);

        sinEvidencia.Error.Code.Should().Be(HabeasDataErrorCodes.EvidenceRequired);
        ajena.Error.Code.Should().Be(HabeasDataErrorCodes.EvidenceNotFound);
        deOtroTipo.Error.Code.Should().Be(HabeasDataErrorCodes.EvidenceNotFound);
        migrada.IsSuccess.Should().BeTrue();
        var vigente = await d.Autorizacion.VigenteAsync(titular.PublicId, DataAuthorizationPurpose.CreditProcessing, null, CancellationToken.None);
        vigente!.HasEvidence.Should().BeFalse("migrada sin evidencia: autoriza el tratamiento y el cobro, no centrales ni mensajes (FR-006)");
        vigente.Autoriza.Should().BeTrue();
    }

    [Fact]
    public async Task Publicar_una_finalidad_no_cierra_las_otras_y_el_numero_corre_por_finalidad()
    {
        var d = new PersonasTestData();
        Politica(d, DataAuthorizationPurpose.GeneralTreatment, version: 1);
        var handler = new PublishPolicyVersionCommandHandler(d.Db, d.User, d.Clock);

        var r = await handler.Handle(new PublishPolicyVersionCommand("Consulta a centrales", "Texto", Ahora.AddMinutes(1),
            DataAuthorizationPurpose.CreditBureauQuery), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        var vigentes = await d.Db.HabeasDataPolicyVersions.Where(p => p.EffectiveTo == null).ToListAsync();
        vigentes.Should().HaveCount(2, "una vigente por finalidad");
        vigentes.Single(p => p.Purpose == DataAuthorizationPurpose.CreditBureauQuery).VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task La_lista_por_PublicId_muestra_el_estado_de_cada_autorizacion()
    {
        var d = new PersonasTestData();
        var titular = d.Persona();
        Politica(d, DataAuthorizationPurpose.ContactChannel);
        await d.Autorizacion.AgregarAsync(new AutorizacionAAgregar(titular.PublicId, DataAuthorizationPurpose.ContactChannel,
            DecisionDeAutorizacion.Accepted, null, DataAuthorizationChannel.Sms, null, DataAuthorizationOrigin.Migration, Ahora.AddDays(-3)), CancellationToken.None);
        await d.Db.SaveChangesAsync();
        await d.Autorizacion.AgregarRevocacionAsync(titular.Id, DataAuthorizationPurpose.ContactChannel, DataAuthorizationChannel.Sms, Ahora, "Pidió no más SMS",
            CancellationToken.None);
        await d.Db.SaveChangesAsync();

        var r = await new ListDataAuthorizationsQueryHandler(d.Db, d.User).Handle(new ListDataAuthorizationsQuery(titular.PublicId), CancellationToken.None);

        var sms = r.Value.Should().ContainSingle().Subject;
        sms.Status.Should().Be(EstadosDeAutorizacion.Revoked);
        sms.RevokedAt.Should().Be(Ahora);
        (await new ListDataAuthorizationsQueryHandler(d.Db, d.User).Handle(new ListDataAuthorizationsQuery(Guid.NewGuid()), CancellationToken.None))
            .Error.Code.Should().Be("Core.Person.NotFound");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Los_unicos_de_la_politica_se_rehacen_con_Purpose_y_el_indice_por_finalidad_existe(bool postgres)
    {
        var modelo = Modelo(postgres);
        var politica = modelo.FindEntityType(typeof(HabeasDataPolicyVersion))!;
        var version = politica.GetIndexes().Single(i => i.GetDatabaseName() == "UK_CMP_HabeasPolicyVersions_TenantVersion");
        version.IsUnique.Should().BeTrue();
        version.Properties.Select(p => p.Name).Should().Equal("TenantId", "Purpose", "VersionNumber");
        var vigente = politica.GetIndexes().Single(i => i.GetDatabaseName() == "UK_CMP_HabeasPolicyVersions_Current");
        vigente.IsUnique.Should().BeTrue();
        vigente.Properties.Select(p => p.Name).Should().Equal("TenantId", "Purpose");
        vigente.GetFilter().Should().Contain("EffectiveTo");

        var consentimiento = modelo.FindEntityType(typeof(HabeasDataConsent))!;
        consentimiento.GetIndexes().Single(i => i.GetDatabaseName() == "IX_CMP_HabeasConsents_Purpose").Properties.Select(p => p.Name)
            .Should().Equal("PersonId", "Purpose", "ContactChannel", "ActionAt");
        consentimiento.FindProperty(nameof(HabeasDataConsent.Purpose))!.GetMaxLength().Should().Be(30);
        consentimiento.FindProperty(nameof(HabeasDataConsent.Origin))!.GetDefaultValue().Should().Be(DataAuthorizationOrigin.PersonRegistration);
    }

    private static IModel Modelo(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres
            ? builder.UseNpgsql("Host=localhost;Database=x;Username=y;Password=z")
            : builder.UseSqlServer("Server=localhost;Database=x;User Id=y;Password=z;TrustServerCertificate=True");
        using var db = new ApplicationDbContext(builder.Options);
        return db.Model;
    }
}
