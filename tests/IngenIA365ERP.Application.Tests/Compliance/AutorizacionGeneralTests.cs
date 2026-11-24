using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Compliance.HabeasData.Authorizations;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Application.Core.People.Commands.CreatePerson;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Tests.Core.People;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Compliance;

/// <summary>
/// 2026-11-24 (pedido del dueño: «una autorización general que permita que el asociado tenga acceso a todo»): la autorización general registra en un
/// paso una decisión por finalidad y canal, cada una contra la política vigente de su finalidad. Con el soporte firmado las cubre todas; sin él sólo
/// las que no lo exigen, y dice cuáles quedaron por fuera y por qué. El tratamiento general nunca implica centrales: cada consumidor sigue
/// preguntando por su finalidad. En el alta de la persona, «todas las finalidades» suma el trámite de crédito al tratamiento general.
/// </summary>
public class AutorizacionGeneralTests
{
    private static readonly DateTime Ahora = PersonasTestData.Ahora;

    private static PersonasTestData Escenario()
    {
        var d = new PersonasTestData();
        d.Clock.HoyLocal.Returns(DateOnly.FromDateTime(Ahora));
        return d;
    }

    private static void Politicas(PersonasTestData d, params DataAuthorizationPurpose[] finalidades)
    {
        foreach (var f in finalidades)
            d.Db.HabeasDataPolicyVersions.Add(new HabeasDataPolicyVersion
            {
                TenantId = 1, VersionNumber = 1, Title = $"{f} v1", ContentMarkdown = "Texto de la política", Sha256Hex = new string('a', 64),
                EffectiveFrom = Ahora.AddMonths(-1), PublishedBy = "admin@demo", CreatedBy = "admin@demo", Purpose = f,
            });
        d.Db.SaveChanges();
    }

    private static void TodasLasPoliticas(PersonasTestData d) => Politicas(d, Enum.GetValues<DataAuthorizationPurpose>());

    private static Attachment Soporte(PersonasTestData d, Person titular)
    {
        var a = new Attachment
        {
            TenantId = 1, OwnerEntityType = AdjuntosDeModulo.AutorizacionDeDatos, OwnerEntityPublicId = titular.PublicId, FileName = "autorizacion.pdf",
            ContentType = "application/pdf", SizeBytes = 10, Sha256Hex = new string('b', 64), StoragePath = "x", Status = EstadoDeAdjunto.Available,
        };
        d.Db.Attachments.Add(a);
        d.Db.SaveChanges();
        return a;
    }

    private static AutorizacionGeneral General(PersonasTestData d) => new(d.Autorizacion, new LectorDeParametros(d.Db), d.Clock);

    private static RecordGeneralDataAuthorizationCommandHandler Registrar(PersonasTestData d) => new(d.Db, General(d), d.Clock);

    private static RecordGeneralDataAuthorizationCommand Comando(Person titular, Guid? soporte = null) =>
        new(titular.PublicId, soporte, DataAuthorizationOrigin.PersonRegistration, Ahora);

    [Fact]
    public void Cubre_las_cuatro_finalidades_y_los_cinco_canales_de_contacto()
    {
        AutorizacionGeneral.Finalidades.Should().HaveCount(9);
        AutorizacionGeneral.Finalidades.Where(f => f.Purpose == DataAuthorizationPurpose.ContactChannel).Select(f => f.ContactChannel)
            .Should().BeEquivalentTo(Enum.GetValues<DataAuthorizationChannel>().Cast<DataAuthorizationChannel?>());
        AutorizacionGeneral.Finalidades.Where(f => f.Purpose != DataAuthorizationPurpose.ContactChannel).Should().OnlyContain(f => f.ContactChannel == null);
    }

    [Fact]
    public async Task Con_el_soporte_registra_una_decision_por_cada_finalidad_y_canal()
    {
        var d = Escenario();
        TodasLasPoliticas(d);
        var titular = d.Persona();

        var r = await Registrar(d).Handle(Comando(titular, Soporte(d, titular).PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Recorded.Should().HaveCount(9);
        r.Value.Omitted.Should().BeEmpty();
        (await d.Db.HabeasDataConsents.CountAsync()).Should().Be(9, "una decisión por finalidad y canal: la trazabilidad sigue siendo por finalidad");
        foreach (var f in new[] { DataAuthorizationPurpose.CreditProcessing, DataAuthorizationPurpose.CreditBureauQuery, DataAuthorizationPurpose.CreditBureauReport })
        {
            var vigente = await d.Autorizacion.VigenteAsync(titular.PublicId, f, null, CancellationToken.None);
            vigente!.Autoriza.Should().BeTrue();
            vigente.HasEvidence.Should().BeTrue();
        }
        (await d.Autorizacion.VigenteAsync(titular.PublicId, DataAuthorizationPurpose.ContactChannel, "WhatsApp", CancellationToken.None))!
            .Autoriza.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_soporte_registra_las_que_no_lo_exigen_y_dice_cuales_quedaron_por_fuera()
    {
        var d = Escenario();
        TodasLasPoliticas(d);
        var titular = d.Persona();

        var r = await Registrar(d).Handle(Comando(titular), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Recorded.Select(x => x.Purpose).Should().BeEquivalentTo([DataAuthorizationPurpose.GeneralTreatment, DataAuthorizationPurpose.CreditProcessing]);
        r.Value.Omitted.Should().HaveCount(7).And.OnlyContain(o => o.Code == HabeasDataErrorCodes.EvidenceRequired && o.Reason.Length > 0);
        (await d.Db.HabeasDataConsents.CountAsync()).Should().Be(2);
        (await d.Autorizacion.VigenteAsync(titular.PublicId, DataAuthorizationPurpose.CreditBureauQuery, null, CancellationToken.None))
            .Should().BeNull("las centrales piden consentimiento expreso con soporte: sin él no quedan");
    }

    [Fact]
    public async Task Una_finalidad_sin_politica_vigente_queda_por_fuera_con_su_motivo()
    {
        var d = Escenario();
        Politicas(d, DataAuthorizationPurpose.GeneralTreatment);
        var titular = d.Persona();

        var r = await Registrar(d).Handle(Comando(titular), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Recorded.Should().ContainSingle(x => x.Purpose == DataAuthorizationPurpose.GeneralTreatment);
        r.Value.Omitted.Should().ContainSingle(o => o.Purpose == DataAuthorizationPurpose.CreditProcessing)
            .Which.Code.Should().Be(HabeasDataErrorCodes.NoCurrentPolicy);
    }

    [Fact]
    public async Task Si_la_cooperativa_exige_el_soporte_y_no_viene_no_registra_ninguna()
    {
        var d = Escenario();
        TodasLasPoliticas(d);
        d.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.AutorizacionesExigenEvidencia, Value = "true",
            ValidFrom = DateOnly.FromDateTime(Ahora).AddDays(-1), Reason = "prueba",
        });
        await d.Db.SaveChangesAsync();
        var titular = d.Persona();

        var r = await Registrar(d).Handle(Comando(titular), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(HabeasDataErrorCodes.EvidenceRequired);
        (await d.Db.HabeasDataConsents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_ninguna_politica_publicada_falla_con_NoCurrentPolicy()
    {
        var d = Escenario();
        var titular = d.Persona();

        var r = await Registrar(d).Handle(Comando(titular, Soporte(d, titular).PublicId), CancellationToken.None);

        r.Error.Code.Should().Be(HabeasDataErrorCodes.NoCurrentPolicy);
        (await d.Db.HabeasDataConsents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_soporte_que_no_es_del_titular_se_rechaza_sin_registrar_nada()
    {
        var d = Escenario();
        TodasLasPoliticas(d);
        var titular = d.Persona();
        var otro = d.Persona("900", "Otra", "Persona");

        var r = await Registrar(d).Handle(Comando(titular, Soporte(d, otro).PublicId), CancellationToken.None);

        r.Error.Code.Should().Be(HabeasDataErrorCodes.EvidenceNotFound);
        (await d.Db.HabeasDataConsents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_tratamiento_general_solo_no_implica_las_demas_finalidades()
    {
        var d = Escenario();
        TodasLasPoliticas(d);
        var titular = d.Persona();

        var r = await new RecordDataAuthorizationCommandHandler(d.Db, d.Autorizacion, d.Clock, new LectorDeParametros(d.Db)).Handle(
            new RecordDataAuthorizationCommand(titular.PublicId, DataAuthorizationPurpose.GeneralTreatment, null, null, null,
                DataAuthorizationOrigin.PersonRegistration, Ahora), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        (await d.Autorizacion.VigenteAsync(titular.PublicId, DataAuthorizationPurpose.CreditProcessing, null, CancellationToken.None)).Should().BeNull();
        (await d.Autorizacion.VigenteAsync(titular.PublicId, DataAuthorizationPurpose.CreditBureauReport, null, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task En_el_alta_todas_las_finalidades_suma_el_tramite_de_credito_al_tratamiento_general()
    {
        var d = Escenario();
        TodasLasPoliticas(d);
        var tratamiento = await d.Db.HabeasDataPolicyVersions.SingleAsync(p => p.Purpose == DataAuthorizationPurpose.GeneralTreatment);
        var altas = new AltaConAutorizacion(d.Db, d.Personas, d.Autorizacion, General(d));

        var r = await new CreatePersonCommandHandler(altas).Handle(PerfilTributarioDePersonaTests.Comando(PersonasTestData.Entrada()) with
        {
            Authorization = new AutorizacionAlCrear(DecisionDeAutorizacion.Accepted, tratamiento.PublicId, "Caja", AllPurposes: true),
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var finalidades = await d.Db.HabeasDataConsents.AsNoTracking().Select(c => c.Purpose).ToListAsync();
        finalidades.Should().BeEquivalentTo([DataAuthorizationPurpose.GeneralTreatment, DataAuthorizationPurpose.CreditProcessing],
            "al crear no hay soporte: centrales y canales de contacto quedan para Autorizaciones del titular");
        (await d.Autorizacion.VigenteAsync(r.Value, DataAuthorizationPurpose.CreditProcessing, null, CancellationToken.None))!.Autoriza.Should().BeTrue();
    }

    [Fact]
    public async Task En_el_alta_solo_el_tratamiento_o_la_negativa_no_registran_mas()
    {
        var d = Escenario();
        TodasLasPoliticas(d);
        var tratamiento = await d.Db.HabeasDataPolicyVersions.SingleAsync(p => p.Purpose == DataAuthorizationPurpose.GeneralTreatment);
        var altas = new AltaConAutorizacion(d.Db, d.Personas, d.Autorizacion, General(d));

        var solo = await new CreatePersonCommandHandler(altas).Handle(PerfilTributarioDePersonaTests.Comando(PersonasTestData.Entrada("111")) with
        {
            Authorization = new AutorizacionAlCrear(DecisionDeAutorizacion.Accepted, tratamiento.PublicId, "Caja"),
        }, CancellationToken.None);
        var negada = await new CreatePersonCommandHandler(altas).Handle(PerfilTributarioDePersonaTests.Comando(PersonasTestData.Entrada("222")) with
        {
            Authorization = new AutorizacionAlCrear(DecisionDeAutorizacion.Declined, tratamiento.PublicId, "Caja", AllPurposes: true),
        }, CancellationToken.None);

        solo.IsSuccess.Should().BeTrue();
        negada.IsSuccess.Should().BeTrue();
        (await d.Db.HabeasDataConsents.CountAsync()).Should().Be(2, "una por alta: la general sólo suma cuando el titular acepta");
    }
}
