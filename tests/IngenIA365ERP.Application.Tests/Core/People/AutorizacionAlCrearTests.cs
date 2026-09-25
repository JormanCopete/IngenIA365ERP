using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Application.Core.Associates.Commands.RegisterAssociateWithPerson;
using IngenIA365ERP.Application.Core.Associates.Contracts;
using IngenIA365ERP.Application.Core.People.Commands.CreatePerson;
using IngenIA365ERP.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// Feature 012, T112 (T46, FR-011, <c>contracts/api.md</c> §29 «Personas»): la autorización de tratamiento de datos
/// capturada al crear una persona (<see cref="AutorizacionAlCrear"/>) queda en el registro de consentimientos que ya existe
/// (<c>CMP_HabeasDataConsents</c>) junto con la persona —las dos o ninguna—; «Declined» es una acción más; sin política
/// publicada el alta procede y deja la constancia «sin política vigente»; y sin autorización nada cambia.
/// </summary>
public class AutorizacionAlCrearTests
{
    private static HabeasDataPolicyVersion Politica(PersonasTestData d, int tenantId = 1, int version = 1, bool vigente = true)
    {
        var p = new HabeasDataPolicyVersion
        {
            TenantId = tenantId,
            VersionNumber = version,
            Title = $"Política v{version}",
            ContentMarkdown = "Tratamos tus datos para…",
            Sha256Hex = new string('a', 64),
            EffectiveFrom = PersonasTestData.Ahora.AddMonths(-version),
            EffectiveTo = vigente ? null : PersonasTestData.Ahora.AddDays(-1),
            PublishedBy = "admin@demo",
            CreatedBy = "admin@demo",
        };
        d.Db.HabeasDataPolicyVersions.Add(p);
        d.Db.SaveChanges();
        return p;
    }

    private static CreatePersonCommand Alta(AutorizacionAlCrear? autorizacion, string taxId = "1023456789") =>
        PerfilTributarioDePersonaTests.Comando(PersonasTestData.Entrada(taxId)) with { Authorization = autorizacion };

    [Fact]
    public async Task Aceptada_escribe_la_persona_y_su_consentimiento_de_esa_version()
    {
        var d = new PersonasTestData();
        var politica = Politica(d);

        var r = await new CreatePersonCommandHandler(d.Altas).Handle(
            Alta(new AutorizacionAlCrear(DecisionDeAutorizacion.Accepted, politica.PublicId, "Pos")), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var persona = await d.Db.People.AsNoTracking().SingleAsync(p => p.PublicId == r.Value);
        var consentimiento = await d.Db.HabeasDataConsents.AsNoTracking().SingleAsync();
        consentimiento.PersonId.Should().Be(persona.Id);
        consentimiento.PolicyVersionId.Should().Be(politica.Id);
        consentimiento.TenantId.Should().Be(1);
        consentimiento.Action.Should().Be(AccionesDeConsentimiento.Accepted);
        consentimiento.Channel.Should().Be("Pos");
        consentimiento.ActionBy.Should().Be("operador@demo");
        consentimiento.ActionAt.Should().Be(PersonasTestData.Ahora);
    }

    [Fact]
    public async Task Negada_escribe_el_consentimiento_con_Action_Declined()
    {
        var d = new PersonasTestData();
        var politica = Politica(d);

        var r = await new CreatePersonCommandHandler(d.Altas).Handle(
            Alta(new AutorizacionAlCrear(DecisionDeAutorizacion.Declined, politica.PublicId, "Compras")), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await d.Db.HabeasDataConsents.AsNoTracking().SingleAsync()).Action.Should().Be(AccionesDeConsentimiento.Declined);
    }

    [Fact]
    public async Task Si_el_consentimiento_no_se_puede_escribir_no_queda_la_persona()
    {
        var d = new PersonasTestData();
        Politica(d);
        var deOtraCooperativa = Politica(d, tenantId: 2);

        var desconocida = await new CreatePersonCommandHandler(d.Altas).Handle(
            Alta(new AutorizacionAlCrear(DecisionDeAutorizacion.Accepted, Guid.NewGuid(), "Pos")), CancellationToken.None);
        var ajena = await new CreatePersonCommandHandler(d.Altas).Handle(
            Alta(new AutorizacionAlCrear(DecisionDeAutorizacion.Accepted, deOtraCooperativa.PublicId, "Pos")), CancellationToken.None);

        desconocida.Error.Code.Should().Be(AutorizacionDeDatos.PoliticaDesconocida);
        ajena.Error.Code.Should().Be(AutorizacionDeDatos.PoliticaDesconocida);
        (await d.Db.People.CountAsync()).Should().Be(0);
        (await d.Db.HabeasDataConsents.CountAsync()).Should().Be(0);
        d.Db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Should().BeEmpty();
    }

    [Fact]
    public async Task Con_politica_vigente_la_decision_exige_la_version_que_se_mostro()
    {
        var d = new PersonasTestData();
        Politica(d);

        var r = await new CreatePersonCommandHandler(d.Altas).Handle(
            Alta(new AutorizacionAlCrear(DecisionDeAutorizacion.Accepted, null, "Pos")), CancellationToken.None);

        r.Error.Code.Should().Be(AutorizacionDeDatos.PoliticaRequerida);
        (await d.Db.People.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_politica_publicada_el_alta_procede_con_la_constancia_sin_politica_vigente()
    {
        var d = new PersonasTestData();

        var r = await new CreatePersonCommandHandler(d.Altas).Handle(
            Alta(new AutorizacionAlCrear(DecisionDeAutorizacion.Accepted, null, "Pos")), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await d.Db.People.CountAsync()).Should().Be(1);
        (await d.Db.HabeasDataConsents.CountAsync()).Should().Be(0, "sin política no hay versión sobre la cual decidir");
        await d.Auditoria.Received(1).LogAsync(
            AuditEventTypes.PersonDataAuthorizationNoCurrentPolicy, "Person", r.Value.ToString(),
            Arg.Any<object?>(), Arg.Is<object?>(o => o != null && o.ToString()!.Contains(AutorizacionDeDatos.SinPoliticaVigente)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Vigente_devuelve_la_ultima_decision_por_PublicId()
    {
        var d = new PersonasTestData();
        var v1 = Politica(d, version: 1, vigente: false);
        var persona = d.Persona("55");
        d.Db.HabeasDataConsents.Add(new HabeasDataConsent
        {
            TenantId = 1, PersonId = persona.Id, PolicyVersionId = v1.Id, Action = AccionesDeConsentimiento.Accepted,
            ActionAt = PersonasTestData.Ahora.AddMonths(-3), ActionBy = "x", Channel = "Web",
        });
        d.Db.HabeasDataConsents.Add(new HabeasDataConsent
        {
            TenantId = 1, PersonId = persona.Id, PolicyVersionId = v1.Id, Action = AccionesDeConsentimiento.Declined,
            ActionAt = PersonasTestData.Ahora.AddMonths(-1), ActionBy = "x", Channel = "Pos",
        });
        await d.Db.SaveChangesAsync();

        var vigente = await d.Autorizacion.VigenteAsync(persona.PublicId, CancellationToken.None);

        vigente.Should().NotBeNull();
        vigente!.Decision.Should().Be(AccionesDeConsentimiento.Declined);
        vigente.Autoriza.Should().BeFalse("Declined se lee como sin autorización");
        vigente.PolicyVersionPublicId.Should().Be(v1.PublicId);
        vigente.PolicyVersionNumber.Should().Be(1);
        vigente.Channel.Should().Be("Pos");
        (await d.Autorizacion.VigenteAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
        (await d.Autorizacion.VigenteAsync(d.Persona("56").PublicId, CancellationToken.None)).Should().BeNull("nunca decidió");
    }

    [Fact]
    public void La_interfaz_sólo_admite_el_PublicId()
    {
        var parametros = typeof(IAutorizacionDeDatos).GetMethod(nameof(IAutorizacionDeDatos.VigenteAsync))!.GetParameters();

        parametros.Select(p => p.ParameterType).Should().Equal(typeof(Guid), typeof(CancellationToken));
    }

    [Fact]
    public async Task Sin_autorizacion_nada_cambia_en_Personas_ni_en_los_compuestos()
    {
        var d = new PersonasTestData();
        Politica(d);

        var persona = await new CreatePersonCommandHandler(d.Altas).Handle(Alta(null), CancellationToken.None);
        var asociado = await new RegisterAssociateWithPersonCommandHandler(d.Altas, d.Asociados).Handle(
            new RegisterAssociateWithPersonCommand(PersonasTestData.Entrada("99"), new AssociateInput { JoinDate = new DateOnly(2026, 9, 15), ContributionRate = 5m }),
            CancellationToken.None);

        persona.IsSuccess.Should().BeTrue(persona.Error.Message);
        asociado.IsSuccess.Should().BeTrue(asociado.Error.Message);
        (await d.Db.People.CountAsync()).Should().Be(2);
        (await d.Db.HabeasDataConsents.CountAsync()).Should().Be(0);
        await d.Auditoria.DidNotReceiveWithAnyArgs().LogAsync(default!, default!, default!, default, default, default);
    }

    [Fact]
    public async Task El_compuesto_con_autorizacion_escribe_persona_rol_y_consentimiento()
    {
        var d = new PersonasTestData();
        var politica = Politica(d);

        var r = await new RegisterAssociateWithPersonCommandHandler(d.Altas, d.Asociados).Handle(
            new RegisterAssociateWithPersonCommand(PersonasTestData.Entrada("77"), new AssociateInput { JoinDate = new DateOnly(2026, 9, 15), ContributionRate = 5m },
                new AutorizacionAlCrear(DecisionDeAutorizacion.Accepted, politica.PublicId, "Oficina")),
            CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var persona = await d.Db.People.AsNoTracking().SingleAsync(p => p.PublicId == r.Value.PersonPublicId);
        persona.IsAssociate.Should().BeTrue();
        (await d.Db.HabeasDataConsents.AsNoTracking().SingleAsync()).PersonId.Should().Be(persona.Id);
    }
}
