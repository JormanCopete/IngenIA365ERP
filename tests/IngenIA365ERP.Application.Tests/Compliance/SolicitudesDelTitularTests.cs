using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Compliance.DataSubjectRequests;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Application.Tests.Core.People;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Common.Compliance;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Compliance;

/// <summary>
/// T0033 (feature 013; T69 b, S10; data-model parte 3 §4.9; contracts/api.md §22.3; contracts/interfaces.md §10.3; quickstart §1.5): las
/// solicitudes del titular. El vencimiento sale de <c>PlazosDelTitular</c> con los límites <c>TITULAR_*</c> y el calendario de días hábiles
/// nacionales; la prórroga es una sola (<c>Compliance.DataSubjectRequest.AlreadyExtended</c>); responder una cerrada es <c>.NotOpen</c>; la
/// revocación de un canal de contacto procede y agrega en el mismo <c>SaveChanges</c> la fila <c>Revoked</c> con <c>Origin = TitularRequest</c>;
/// la de una finalidad de crédito consulta el deber (el doble de la consulta de obligaciones) y, con una obligación viva, responde
/// <c>Denied</c> con la constancia sellada en <c>DutyEvidenceJson</c>.
/// </summary>
public class SolicitudesDelTitularTests
{
    /// <summary>Viernes 2 de octubre de 2026, 10:00 en Colombia.</summary>
    private static readonly DateTime Recibida = new(2026, 10, 2, 15, 0, 0, DateTimeKind.Utc);

    /// <summary>Lunes festivo (Día de la Raza).</summary>
    private static readonly DateOnly Festivo = new(2026, 10, 12);

    private readonly PersonasTestData _d = new();
    private readonly IValoresNacionales _valores = Substitute.For<IValoresNacionales>();
    private readonly ICalendarioHabil _calendario = Substitute.For<ICalendarioHabil>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IDeberDeConservacionDelTitular _deber = Substitute.For<IDeberDeConservacionDelTitular>();
    private readonly Person _titular;

    public SolicitudesDelTitularTests()
    {
        _d.Clock.UtcNow.Returns(Recibida.AddDays(3));
        _d.Clock.HoyLocal.Returns(DateOnly.FromDateTime(Recibida.AddDays(3)));
        Limite(CatalogoDeLimitesLegales.TitularConsultaDias, 10);
        Limite(CatalogoDeLimitesLegales.TitularConsultaProrrogaDias, 5);
        Limite(CatalogoDeLimitesLegales.TitularReclamoDias, 15);
        Limite(CatalogoDeLimitesLegales.TitularReclamoProrrogaDias, 8);
        _calendario.ObtenerAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<IReadOnlyCollection<DayOfWeek>>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new CalendarioHabil([Festivo], ci.ArgAt<IReadOnlyCollection<DayOfWeek>>(2))));
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(), "Oficial de cumplimiento",
            "oficial@coop.test", ExecutionChannel.App, "POST /api/compliance/data-subject-requests", "10.0.0.1", null));
        _deber.ConsultarAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new DeberDelTitular(false, [], SinObligacionesRegistradas.Fuente, ci.ArgAt<DateOnly>(1))));
        _titular = _d.Persona();
    }

    private void Limite(string clave, int dias) =>
        _valores.LeerAsync(CodigosDeValorNacional.Limite, clave, Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Limite, clave, dias, "Dias", null,
                new DateOnly(2026, 1, 1), null, null, "Ley 1581 de 2012", 1, Recibida.AddYears(-1), EstadoDeRevision.Validada)));

    private Task<Result<DataSubjectRequestDto>> Radicar(DataSubjectRequestKind clase, DataAuthorizationPurpose? finalidad = null,
        DataAuthorizationChannel? canal = null) =>
        new RegisterDataSubjectRequestCommandHandler(_d.Db, _valores, _calendario, _actor, _d.Clock).Handle(
            new RegisterDataSubjectRequestCommand(_titular.PublicId, clase, finalidad, canal, "Escrito", Recibida, "Lo que pide el titular."),
            CancellationToken.None);

    private Task<Result<DataSubjectRequestDto>> Responder(Guid solicitud, DataSubjectRequestOutcome desenlace = DataSubjectRequestOutcome.Granted) =>
        new RespondDataSubjectRequestCommandHandler(_d.Db, _deber, _valores, _d.Autorizacion, _actor, _d.Clock).Handle(
            new RespondDataSubjectRequestCommand(solicitud, desenlace, "Respuesta al titular."), CancellationToken.None);

    private Task<Result<DataSubjectRequestDto>> Prorrogar(Guid solicitud) =>
        new ExtendDataSubjectRequestCommandHandler(_d.Db, _valores, _calendario).Handle(
            new ExtendDataSubjectRequestCommand(solicitud, "Hace falta consultar el archivo físico."), CancellationToken.None);

    private void PoliticaDeContacto()
    {
        _d.Db.HabeasDataPolicyVersions.Add(new HabeasDataPolicyVersion
        {
            TenantId = 1, VersionNumber = 1, Title = "Canales", ContentMarkdown = "x", Sha256Hex = new string('a', 64),
            EffectiveFrom = Recibida.AddMonths(-1), PublishedBy = "admin", Purpose = DataAuthorizationPurpose.ContactChannel,
        });
        _d.Db.SaveChanges();
    }

    [Fact]
    public async Task La_consulta_vence_a_los_dias_habiles_de_consulta_sin_contar_festivos_ni_fines_de_semana()
    {
        var r = await Radicar(DataSubjectRequestKind.Query);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.DueDate.Should().Be(new DateOnly(2026, 10, 19), "10 hábiles desde el viernes 2, saltando el festivo del 12");
        r.Value.Status.Should().Be(DataSubjectRequestStatus.Received);
        var guardada = await _d.Db.DataSubjectRequests.SingleAsync();
        guardada.ReceivedOn.Should().Be(new DateOnly(2026, 10, 2));
        guardada.RegisteredByUserId.Should().Be(7);
        guardada.DueDate.Should().Be(PlazosDelTitular.Vencimiento(DataSubjectRequestKind.Query, guardada.ReceivedOn,
            new LimitesDelTitular(10, 5, 15, 8), new CalendarioHabil([Festivo], DiasDeDescanso.SabadoYDomingo)));
    }

    [Fact]
    public async Task Un_reclamo_vence_con_el_plazo_de_reclamo_y_se_prorroga_una_sola_vez()
    {
        var r = await Radicar(DataSubjectRequestKind.Rectification);
        r.Value.DueDate.Should().Be(new DateOnly(2026, 10, 26));

        var prorroga = await Prorrogar(r.Value.PublicId);
        prorroga.IsSuccess.Should().BeTrue();
        prorroga.Value.ExtendedDueDate.Should().Be(new DateOnly(2026, 11, 5));
        prorroga.Value.Status.Should().Be(DataSubjectRequestStatus.Extended);

        var otra = await Prorrogar(r.Value.PublicId);
        otra.IsFailure.Should().BeTrue();
        otra.Error.Code.Should().Be(ErroresDeSolicitudesDelTitular.CodigoYaProrrogada);
    }

    [Fact]
    public async Task Responder_una_cerrada_es_NotOpen()
    {
        var r = await Radicar(DataSubjectRequestKind.Query);
        (await Responder(r.Value.PublicId)).IsSuccess.Should().BeTrue();

        var otra = await Responder(r.Value.PublicId);

        otra.IsFailure.Should().BeTrue();
        otra.Error.Code.Should().Be(ErroresDeSolicitudesDelTitular.CodigoNoAbierta);
        (await Prorrogar(r.Value.PublicId)).Error.Code.Should().Be(ErroresDeSolicitudesDelTitular.CodigoNoAbierta);
    }

    [Fact]
    public async Task Revocar_un_canal_de_contacto_procede_y_agrega_la_fila_Revoked_en_el_mismo_guardado()
    {
        PoliticaDeContacto();
        var r = await Radicar(DataSubjectRequestKind.Revocation, DataAuthorizationPurpose.ContactChannel, DataAuthorizationChannel.WhatsApp);

        var respuesta = await Responder(r.Value.PublicId);

        respuesta.IsSuccess.Should().BeTrue(respuesta.IsFailure ? respuesta.Error.Message : null);
        respuesta.Value.Outcome.Should().Be(DataSubjectRequestOutcome.Granted);
        await _deber.DidNotReceiveWithAnyArgs().ConsultarAsync(default, default, default);
        var revocada = await _d.Db.HabeasDataConsents.AsNoTracking().SingleAsync();
        revocada.Action.Should().Be(AccionesDeConsentimiento.Revoked);
        revocada.Purpose.Should().Be(DataAuthorizationPurpose.ContactChannel);
        revocada.ContactChannel.Should().Be(DataAuthorizationChannel.WhatsApp);
        revocada.Origin.Should().Be(DataAuthorizationOrigin.TitularRequest);
        revocada.PersonId.Should().Be(_titular.Id);
        var solicitud = await _d.Db.DataSubjectRequests.AsNoTracking().SingleAsync();
        solicitud.Status.Should().Be(DataSubjectRequestStatus.Answered);
        solicitud.RespondedAt.Should().Be(revocada.ActionAt, "la respuesta y la revocación salen del mismo guardado");
    }

    [Fact]
    public async Task Revocar_una_finalidad_de_credito_con_obligacion_viva_es_Denied_con_la_constancia_sellada()
    {
        _deber.ConsultarAsync(_titular.PublicId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new DeberDelTitular(false, [new ObligacionParaRevocacion("CR-000123", true, null, 0)],
                "Doble de IConsultaDeObligaciones", ci.ArgAt<DateOnly>(1))));
        var r = await Radicar(DataSubjectRequestKind.Revocation, DataAuthorizationPurpose.CreditBureauReport);

        var respuesta = await Responder(r.Value.PublicId, DataSubjectRequestOutcome.Granted);

        respuesta.Value.Outcome.Should().Be(DataSubjectRequestOutcome.Denied, "hay deber legal mientras la obligación exista");
        await _deber.Received(1).ConsultarAsync(_titular.PublicId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        var solicitud = await _d.Db.DataSubjectRequests.AsNoTracking().SingleAsync();
        solicitud.DutyBasis.Should().Contain("CR-000123");
        solicitud.DutyEvidenceJson.Should().Contain("CR-000123").And.Contain("Doble de IConsultaDeObligaciones").And.Contain("\"procede\":false");
        (await _d.Db.HabeasDataConsents.CountAsync()).Should().Be(0, "no se revoca nada");
    }

    [Fact]
    public async Task Sin_obligaciones_registradas_la_revocacion_de_credito_procede_y_lo_deja_dicho()
    {
        _d.Db.HabeasDataPolicyVersions.Add(new HabeasDataPolicyVersion
        {
            TenantId = 1, VersionNumber = 1, Title = "Estudio", ContentMarkdown = "x", Sha256Hex = new string('a', 64),
            EffectiveFrom = Recibida.AddMonths(-1), PublishedBy = "admin", Purpose = DataAuthorizationPurpose.CreditProcessing,
        });
        _d.Db.SaveChanges();
        var r = await Radicar(DataSubjectRequestKind.Suppression, DataAuthorizationPurpose.CreditProcessing);

        var respuesta = await Responder(r.Value.PublicId);

        respuesta.Value.Outcome.Should().Be(DataSubjectRequestOutcome.Granted);
        (await _d.Db.DataSubjectRequests.AsNoTracking().SingleAsync()).DutyEvidenceJson.Should().Contain(SinObligacionesRegistradas.Fuente);
        (await _d.Db.HabeasDataConsents.AsNoTracking().SingleAsync()).Action.Should().Be(AccionesDeConsentimiento.Revoked);
    }

    [Fact]
    public async Task El_defecto_del_deber_responde_sin_obligaciones_registradas()
    {
        var deber = await new SinObligacionesRegistradas().ConsultarAsync(Guid.NewGuid(), new DateOnly(2026, 10, 6), CancellationToken.None);

        deber.Obligaciones.Should().BeEmpty();
        deber.MigradaSinActivar.Should().BeFalse();
        deber.Fuente.Should().Be(SinObligacionesRegistradas.Fuente);
    }

    [Fact]
    public void Revocar_o_suprimir_exige_la_finalidad_y_el_canal_va_solo_con_ContactChannel()
    {
        var v = new RegisterDataSubjectRequestCommandValidator();
        RegisterDataSubjectRequestCommand Con(DataSubjectRequestKind k, DataAuthorizationPurpose? p, DataAuthorizationChannel? c, string medio = "Escrito") =>
            new(Guid.NewGuid(), k, p, c, medio, Recibida, "x");

        v.Validate(Con(DataSubjectRequestKind.Revocation, null, null)).IsValid.Should().BeFalse();
        v.Validate(Con(DataSubjectRequestKind.Revocation, DataAuthorizationPurpose.ContactChannel, null)).IsValid.Should().BeFalse();
        v.Validate(Con(DataSubjectRequestKind.Revocation, DataAuthorizationPurpose.CreditProcessing, DataAuthorizationChannel.Sms)).IsValid.Should().BeFalse();
        v.Validate(Con(DataSubjectRequestKind.Query, null, null, "Paloma")).IsValid.Should().BeFalse();
        v.Validate(Con(DataSubjectRequestKind.Revocation, DataAuthorizationPurpose.ContactChannel, DataAuthorizationChannel.Sms)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_tarea_diaria_alerta_las_solicitudes_que_vencen_pronto()
    {
        var r = await Radicar(DataSubjectRequestKind.Query);
        var alertas = Substitute.For<IAlertas>();
        var levantadas = new List<AlertaALevantar>();
        alertas.LevantarAsync(Arg.Do<AlertaALevantar>(levantadas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
        var handler = new AlertDataSubjectRequestDeadlinesCommandHandler(_d.Db, alertas, _d.Clock);

        _d.Clock.HoyLocal.Returns(new DateOnly(2026, 10, 10));
        (await handler.Handle(new AlertDataSubjectRequestDeadlinesCommand(), CancellationToken.None)).Value.Should().Be(0, "faltan más de 3 días");
        _d.Clock.HoyLocal.Returns(new DateOnly(2026, 10, 17));
        (await handler.Handle(new AlertDataSubjectRequestDeadlinesCommand(), CancellationToken.None)).Value.Should().Be(1);

        levantadas.Should().ContainSingle().Which.DedupKey.Should().Be($"{TiposDeAlerta.PlazoDelTitularPorVencer}:{r.Value.PublicId:D}");
        new TareaDePlazosDelTitular().Nombre.Should().Be("cumplimiento.plazos-del-titular");
    }
}
