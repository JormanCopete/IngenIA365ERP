using FluentAssertions;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Compliance.HabeasData.Authorizations;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Application.Compliance.HabeasData.PublishPolicyVersion;
using IngenIA365ERP.Application.Tests.Core.People;
using IngenIA365ERP.Domain.Enums.Compliance;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Compliance.HabeasData;

/// <summary>
/// 2026-11-24 (pedido del dueño, visto en producción): cada finalidad necesita su política publicada y la pantalla sólo publicaba la de tratamiento
/// general, así que registrar cualquier otra autorización respondía «…no tiene una política vigente para la finalidad GeneralTreatment…». Ahora la
/// misma política se publica para varias finalidades en un paso (una versión por finalidad), el mensaje nombra la finalidad en español y la
/// pantalla donde se publica, y con eso la autorización general registra lo que antes quedaba por fuera.
/// </summary>
public class PoliticaParaVariasFinalidadesTests
{
    private static readonly DateTime Ahora = PersonasTestData.Ahora;

    private static PersonasTestData Escenario()
    {
        var d = new PersonasTestData();
        d.Clock.HoyLocal.Returns(DateOnly.FromDateTime(Ahora));
        return d;
    }

    private static PublishPolicyVersionsCommand Todas(string titulo = "Política de datos") =>
        new(titulo, "Tratamos tus datos para…", DateTime.UtcNow.AddMinutes(1), Enum.GetValues<DataAuthorizationPurpose>());

    [Fact]
    public async Task Publica_una_version_por_finalidad_en_un_paso()
    {
        var d = Escenario();

        var r = await new PublishPolicyVersionsCommandHandler(d.Db, d.User).Handle(Todas(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Select(p => p.Purpose).Should().BeEquivalentTo(Enum.GetValues<DataAuthorizationPurpose>());
        r.Value.Should().OnlyContain(p => p.VersionNumber == 1, "el número corre por finalidad");
        var vigentes = await d.Db.HabeasDataPolicyVersions.AsNoTracking().Where(p => p.EffectiveTo == null).ToListAsync();
        vigentes.Should().HaveCount(5);
        vigentes.Select(p => p.Sha256Hex).Distinct().Should().ContainSingle("es el mismo texto");
    }

    [Fact]
    public async Task Publicar_otra_vez_cierra_la_vigente_de_cada_finalidad_y_sigue_su_numero()
    {
        var d = Escenario();
        var handler = new PublishPolicyVersionsCommandHandler(d.Db, d.User);
        (await handler.Handle(Todas("v1"), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var r = await handler.Handle(Todas("v2") with { Purposes = [DataAuthorizationPurpose.CreditProcessing] }, CancellationToken.None);

        r.Value.Should().ContainSingle().Which.VersionNumber.Should().Be(2);
        var credito = await d.Db.HabeasDataPolicyVersions.AsNoTracking().Where(p => p.Purpose == DataAuthorizationPurpose.CreditProcessing)
            .OrderBy(p => p.VersionNumber).ToListAsync();
        credito.Select(p => (p.VersionNumber, Vigente: p.EffectiveTo == null)).Should().Equal((1, false), (2, true));
        (await d.Db.HabeasDataPolicyVersions.CountAsync(p => p.Purpose == DataAuthorizationPurpose.GeneralTreatment)).Should().Be(1,
            "publicar una finalidad no toca las otras");
    }

    [Fact]
    public void Sin_finalidades_o_repetidas_no_pasa_la_validacion()
    {
        var v = new PublishPolicyVersionsCommandValidator();

        v.Validate(Todas() with { Purposes = [] }).IsValid.Should().BeFalse();
        v.Validate(Todas() with { Purposes = [DataAuthorizationPurpose.GeneralTreatment, DataAuthorizationPurpose.GeneralTreatment] }).IsValid.Should().BeFalse();
        v.Validate(Todas()).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_politica_el_mensaje_nombra_la_finalidad_en_espanol_y_donde_publicarla()
    {
        var d = Escenario();
        var titular = d.Persona();

        var r = await new RecordDataAuthorizationCommandHandler(d.Db, d.Autorizacion, d.Clock, new LectorDeParametros(d.Db)).Handle(
            new RecordDataAuthorizationCommand(titular.PublicId, DataAuthorizationPurpose.GeneralTreatment, null, null, null,
                DataAuthorizationOrigin.PersonRegistration, Ahora), CancellationToken.None);

        r.Error.Code.Should().Be(HabeasDataErrorCodes.NoCurrentPolicy);
        r.Error.Message.Should().Contain("«Tratamiento general»").And.Contain("Cumplimiento › Políticas de datos").And.NotContain("GeneralTreatment");
    }

    [Fact]
    public async Task Con_la_politica_para_todas_la_autorizacion_general_sin_soporte_registra_tratamiento_y_tramite()
    {
        var d = Escenario();
        (await new PublishPolicyVersionsCommandHandler(d.Db, d.User).Handle(Todas(), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var titular = d.Persona();

        var r = await new RecordGeneralDataAuthorizationCommandHandler(d.Db, new AutorizacionGeneral(d.Autorizacion, new LectorDeParametros(d.Db), d.Clock),
            d.Clock).Handle(new RecordGeneralDataAuthorizationCommand(titular.PublicId, null, DataAuthorizationOrigin.PersonRegistration, Ahora),
            CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Recorded.Select(x => x.Purpose).Should().BeEquivalentTo([DataAuthorizationPurpose.GeneralTreatment, DataAuthorizationPurpose.CreditProcessing]);
        r.Value.Omitted.Should().OnlyContain(o => o.Code == HabeasDataErrorCodes.EvidenceRequired, "ya no falta ninguna política: sólo el soporte");
    }
}
