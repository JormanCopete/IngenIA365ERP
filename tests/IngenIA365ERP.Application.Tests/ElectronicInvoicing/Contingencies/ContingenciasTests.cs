using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.Tests.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Contingencies;

/// <summary>
/// Feature 012, I4, T681 (FR-067; contracts/dian.md §7; api.md §24.6): las contingencias. La 04 no la declara una persona
/// (<c>ElectronicInvoicing.Contingency.Dian04OnlyByChannel</c>); una segunda 03 abierta en el canal → <c>.AlreadyOpen</c>; cerrar fija
/// <c>DeadlineAt</c>, <c>DeadlineHoursApplied</c> y <c>LegalSource</c> del parámetro vigente y el <c>TransmissionDeadline</c> de cada documento
/// por <c>PlazoDeContingencia</c> (factura desde el fin, documento soporte desde las 00:00 del día siguiente); la 04 se cierra sola con la
/// primera respuesta definitiva posterior a su inicio.
/// </summary>
public class ContingenciasTests
{
    private const string Norma = "ET art. 616-1; Res. 165/2023 y 167/2021 compiladas en la Res. 000227/2025";

    private readonly EscenarioDeEmision _s = new();
    private readonly IActorActual _persona = Substitute.For<IActorActual>();

    public ContingenciasTests()
    {
        _persona.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(), "Ana Cajera",
            null, ExecutionChannel.Web, "prueba", null, null));
    }

    private OpenContingencyCommandHandler Abrir() => new(_s.E.Db, _s.Contingencias(), _persona, _s.E.Reloj);

    private CloseContingencyCommandHandler Cerrar() => new(_s.E.Db, _s.Contingencias(), _persona, _s.E.Reloj);

    private void Plazo(int horas = 48)
    {
        _s.E.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeFacturacionElectronica.Modulo, Key = ParametrosDeFacturacionElectronica.PlazoContingenciaHoras,
            ScopeKind = ParameterScopeKind.None, Value = horas.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", LegalSource = Norma,
        });
        _s.E.Db.SaveChanges();
    }

    private async Task<Result<DianContingencyEventDto>> Abrir03Async(DateTime? inicio = null) =>
        await Abrir().Handle(new OpenContingencyCommand(ContingencyType.Issuer03, "Se cayó el internet de la sede", inicio, "Foto del módem"), default);

    /// <summary>Un documento del evento, en el estado de contingencia del tipo del evento.</summary>
    private async Task<ElectronicDocument> DelEventoAsync(DianContingencyEvent evento, long consecutivo, ElectronicDocumentKind tipo = ElectronicDocumentKind.Invoice)
    {
        var d = await _s.FacturaAsync(consecutivo);
        d.Kind = tipo;
        d.Status = evento.Type == ContingencyType.Issuer03 ? ElectronicDocumentStatus.IssuerContingency : ElectronicDocumentStatus.DianContingency;
        d.ContingencyType = evento.Type;
        d.ContingencyEventId = evento.Id;
        d.NextAttemptAt = null;
        await _s.E.Db.SaveChangesAsync();
        return d;
    }

    // ------------------------------------------------------------------------------------------------------------ abrir --

    [Fact]
    public async Task Una_persona_no_declara_la_04_Dian04OnlyByChannel()
    {
        var r = await Abrir().Handle(new OpenContingencyCommand(ContingencyType.Dian04, "La DIAN no responde"), default);

        r.Error.Code.Should().Be("ElectronicInvoicing.Contingency.Dian04OnlyByChannel");
        (await _s.E.Db.DianContingencyEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_03_se_abre_en_el_canal_vigente_con_quien_la_declaro_y_levanta_la_alerta()
    {
        var r = await Abrir03Async();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.Type.Should().Be(ContingencyType.Issuer03);
        r.Value.ChannelCode.Should().Be("SIMULADO");
        r.Value.IsOpen.Should().BeTrue();
        r.Value.StartedAt.Should().Be(_s.Ahora);
        r.Value.DetectedBy.Should().Be(new QuienDetectoDto(ActorKind.Person, "Ana Cajera"));
        r.Value.Reason.Should().Contain("Se cayó el internet").And.Contain("Foto del módem");
        var e = await _s.E.Db.DianContingencyEvents.SingleAsync();
        e.DetectedByUserId.Should().Be(7);
        await _s.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.ContingenciaAbierta && a.EntityPublicId == e.PublicId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_segunda_03_abierta_en_el_canal_responde_AlreadyOpen()
    {
        var primera = await Abrir03Async();

        var r = await Abrir03Async();

        r.Error.Code.Should().Be("ElectronicInvoicing.Contingency.AlreadyOpen");
        System.Text.Json.JsonSerializer.Serialize(((ErrorConDatos)r.Error).Data).Should().Contain(primera.Value.ContingencyPublicId.ToString());
        (await _s.E.Db.DianContingencyEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Una_03_con_la_04_abierta_si_se_abre_son_de_tipos_distintos()
    {
        _s.E.Db.DianContingencyEvents.Add(new DianContingencyEvent
        {
            Type = ContingencyType.Dian04, ChannelCode = "SIMULADO", StartedAt = _s.Ahora.AddHours(-1), DetectedByName = "Proceso de integración",
            Reason = "canal", Status = ContingencyEventStatus.Open,
        });
        await _s.E.Db.SaveChangesAsync();

        (await Abrir03Async()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_03_no_empieza_en_el_futuro()
    {
        (await Abrir03Async(_s.Ahora.AddMinutes(5))).Error.Code.Should().Be(ErroresDeContingencias.InvalidDatesCode);
    }

    // ------------------------------------------------------------------------------------------------------------ cerrar --

    [Fact]
    public async Task Cerrar_fija_el_plazo_con_las_horas_y_la_norma_vigentes_y_el_de_cada_documento_por_su_tipo()
    {
        Plazo(48);
        var abierta = await Abrir03Async(_s.Ahora.AddHours(-3));
        var evento = await _s.E.Db.DianContingencyEvents.SingleAsync();
        var factura = await DelEventoAsync(evento, 990000200);
        var soporte = await DelEventoAsync(evento, 990000201, ElectronicDocumentKind.SupportDocument);
        var fin = new DateTime(2026, 12, 5, 16, 0, 0, DateTimeKind.Utc); // 11:00 en Colombia
        _s.Ahora = fin.AddMinutes(30);

        var r = await Cerrar().Handle(new CloseContingencyCommand(abierta.Value.ContingencyPublicId, "Volvió el internet", fin), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.IsOpen.Should().BeFalse();
        r.Value.EndedAt.Should().Be(fin);
        r.Value.DeadlineAt.Should().Be(fin.AddHours(48));
        r.Value.Documents.Should().Be(new DocumentosDeLaContingenciaDto(2, 2, 0, 0));

        _s.E.Db.DescartarCambios();
        var e = await _s.E.Db.DianContingencyEvents.SingleAsync();
        e.Status.Should().Be(ContingencyEventStatus.Closed);
        e.DeadlineHoursApplied.Should().Be(48);
        e.LegalSource.Should().Be(Norma);
        e.ClosedByName.Should().Be("Ana Cajera");
        e.CloseReason.Should().Be("Volvió el internet");

        var f = await _s.E.Db.ElectronicDocuments.SingleAsync(d => d.Id == factura.Id);
        f.TransmissionDeadline.Should().Be(fin.AddHours(48), "la factura cuenta desde el fin del evento");
        f.NextAttemptAt.Should().Be(fin, "queda en la cola del procesador");
        var ds = await _s.E.Db.ElectronicDocuments.SingleAsync(d => d.Id == soporte.Id);
        ds.TransmissionDeadline.Should().Be(new DateTime(2026, 12, 6, 5, 0, 0, DateTimeKind.Utc).AddHours(48),
            "el documento soporte cuenta desde las 00:00 (hora de Colombia) del día siguiente");
    }

    [Fact]
    public async Task Las_horas_del_plazo_salen_del_parametro_no_de_una_constante()
    {
        Plazo(24);
        var abierta = await Abrir03Async(_s.Ahora.AddHours(-1));

        var r = await Cerrar().Handle(new CloseContingencyCommand(abierta.Value.ContingencyPublicId, "Volvió"), default);

        r.Value.DeadlineAt.Should().Be(_s.Ahora.AddHours(24));
        (await _s.E.Db.DianContingencyEvents.AsNoTracking().SingleAsync()).DeadlineHoursApplied.Should().Be(24);
    }

    [Fact]
    public async Task No_se_cierra_dos_veces_ni_antes_de_empezar_ni_en_el_futuro()
    {
        var abierta = await Abrir03Async(_s.Ahora.AddHours(-1));
        var id = abierta.Value.ContingencyPublicId;

        (await Cerrar().Handle(new CloseContingencyCommand(id, "x", _s.Ahora.AddHours(-2)), default)).Error.Code.Should().Be(ErroresDeContingencias.InvalidDatesCode);
        (await Cerrar().Handle(new CloseContingencyCommand(id, "x", _s.Ahora.AddHours(1)), default)).Error.Code.Should().Be(ErroresDeContingencias.InvalidDatesCode);
        (await Cerrar().Handle(new CloseContingencyCommand(id, "x"), default)).IsSuccess.Should().BeTrue();
        (await Cerrar().Handle(new CloseContingencyCommand(id, "x"), default)).Error.Code.Should().Be(ErroresDeContingencias.AlreadyClosedCode);
        (await Cerrar().Handle(new CloseContingencyCommand(Guid.NewGuid(), "x"), default)).Error.Code.Should().Be(ErroresDeContingencias.NotFoundCode);
    }

    [Fact]
    public async Task La_04_tambien_se_cierra_a_mano_con_motivo()
    {
        var evento = new DianContingencyEvent
        {
            Type = ContingencyType.Dian04, ChannelCode = "SIMULADO", StartedAt = _s.Ahora.AddHours(-2), DetectedByName = "Proceso de integración",
            Reason = "canal", Status = ContingencyEventStatus.Open,
        };
        _s.E.Db.DianContingencyEvents.Add(evento);
        await _s.E.Db.SaveChangesAsync();
        var d = await DelEventoAsync(evento, 990000300);

        var r = await Cerrar().Handle(new CloseContingencyCommand(evento.PublicId, "La DIAN confirmó el restablecimiento"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        (await _s.E.Db.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == d.Id)).TransmissionDeadline.Should().NotBeNull();
    }

    [Fact]
    public async Task La_04_se_cierra_sola_con_la_primera_respuesta_definitiva_posterior_a_su_inicio()
    {
        var antes = new DianContingencyEvent
        {
            Type = ContingencyType.Dian04, ChannelCode = "SIMULADO", StartedAt = _s.Ahora.AddHours(-1), DetectedByName = "Proceso de integración",
            Reason = "canal", Status = ContingencyEventStatus.Open,
        };
        var otroCanal = new DianContingencyEvent
        {
            Type = ContingencyType.Dian04, ChannelCode = "PROV", StartedAt = _s.Ahora.AddHours(-1), DetectedByName = "Proceso de integración",
            Reason = "canal", Status = ContingencyEventStatus.Open,
        };
        var la03 = new DianContingencyEvent
        {
            Type = ContingencyType.Issuer03, ChannelCode = "SIMULADO", StartedAt = _s.Ahora.AddHours(-1), DetectedByName = "Ana",
            Reason = "sin internet", Status = ContingencyEventStatus.Open,
        };
        _s.E.Db.DianContingencyEvents.AddRange(antes, otroCanal, la03);
        await _s.E.Db.SaveChangesAsync();

        var cerrados = await _s.Contingencias().CerrarPorRespuestaAsync("SIMULADO", _s.Ahora, default);
        await _s.E.Db.SaveChangesAsync();

        cerrados.Should().ContainSingle().Which.Id.Should().Be(antes.Id);
        antes.Status.Should().Be(ContingencyEventStatus.Closed);
        antes.EndedAt.Should().Be(_s.Ahora);
        antes.ClosedByKind.Should().Be(ActorKind.Process);
        otroCanal.Status.Should().Be(ContingencyEventStatus.Open, "sólo los del canal que respondió");
        la03.Status.Should().Be(ContingencyEventStatus.Open, "la 03 no la cierra la DIAN");

        // Una respuesta anterior al inicio de un evento no lo cierra.
        var nuevo = new DianContingencyEvent
        {
            Type = ContingencyType.Dian04, ChannelCode = "SIMULADO", StartedAt = _s.Ahora.AddMinutes(10), DetectedByName = "Proceso de integración",
            Reason = "canal", Status = ContingencyEventStatus.Open,
        };
        _s.E.Db.DianContingencyEvents.Add(nuevo);
        await _s.E.Db.SaveChangesAsync();
        (await _s.Contingencias().CerrarPorRespuestaAsync("SIMULADO", _s.Ahora, default)).Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------------------------------ consultas --

    [Fact]
    public async Task La_bandeja_filtra_y_el_detalle_trae_documentos_y_evidencia()
    {
        var abierta = await Abrir03Async(_s.Ahora.AddHours(-1));
        var evento = await _s.E.Db.DianContingencyEvents.SingleAsync();
        await DelEventoAsync(evento, 990000400);
        var cerrada = new DianContingencyEvent
        {
            Type = ContingencyType.Dian04, ChannelCode = "SIMULADO", StartedAt = _s.Ahora.AddDays(-3), EndedAt = _s.Ahora.AddDays(-2),
            DetectedByName = "Proceso de integración", Reason = "canal", Status = ContingencyEventStatus.Closed,
        };
        _s.E.Db.DianContingencyEvents.Add(cerrada);
        await _s.E.Db.SaveChangesAsync();

        var abiertas = await new ListContingenciesQueryHandler(_s.E.Db).Handle(new ListContingenciesQuery(IsOpen: true), default);
        abiertas.Value.Should().ContainSingle().Which.Documents.Should().Be(new DocumentosDeLaContingenciaDto(1, 1, 0, 0));
        (await new ListContingenciesQueryHandler(_s.E.Db).Handle(new ListContingenciesQuery(Type: ContingencyType.Dian04), default))
            .Value.Should().ContainSingle().Which.ContingencyPublicId.Should().Be(cerrada.PublicId);
        (await new ListContingenciesQueryHandler(_s.E.Db).Handle(new ListContingenciesQuery(), default)).Value.Should().HaveCount(2);

        var detalle = await new GetContingencyQueryHandler(_s.E.Db, []).Handle(new GetContingencyQuery(abierta.Value.ContingencyPublicId), default);
        detalle.IsSuccess.Should().BeTrue(detalle.IsFailure ? detalle.Error.Message : "");
        detalle.Value.Documents.Should().ContainSingle().Which.Status.Should().Be(ElectronicDocumentStatus.IssuerContingency);
        detalle.Value.Evidence.Should().ContainSingle().Which.Note.Should().Contain("Foto del módem");

        (await new GetContingencyQueryHandler(_s.E.Db, []).Handle(new GetContingencyQuery(Guid.NewGuid()), default))
            .Error.Code.Should().Be(ErroresDeContingencias.NotFoundCode);
    }
}
