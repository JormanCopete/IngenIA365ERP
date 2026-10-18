using FluentAssertions;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Collections.Campaigns;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// Las campañas de mensajes de cobranza (feature 013, K4, T0928; US12-4, api.md §14.9): la vista previa dice quiénes reciben y a quiénes se excluye
/// y por qué; programar crea un mensaje por destinatario con su campaña y, el excluido, con su evento <c>Excluded</c> y motivo (sin autorización del canal
/// → <c>ChannelNotAuthorized</c>); el deudor en insolvencia no recibe; sólo se escribe al deudor, nunca a codeudores ni a referencias; cancelar escribe
/// <c>Cancelled</c> en lo que sigue en cola; el estado avanza con el cierre del ciclo. (nuevo)
/// </summary>
public sealed class CampanasTests : IAsyncLifetime
{
    private readonly CobranzaDePrueba _c = new();
    private Person _dora = null!;
    private Person _edgar = null!;
    private Loan _creditoDeDora = null!;
    private Loan _creditoDeEdgar = null!;

    public Task InitializeAsync()
    {
        _c.E.Autorizar(_c.Ana, MessageChannel.Sms);
        _dora = _c.G.Persona("Dora", "SinAutorizacion", "52111111");
        _dora.Mobile = "3001112223";
        _edgar = _c.G.Persona("Edgar", "Insolvente", "52222222");
        _edgar.Mobile = "3002223334";
        _c.Db.SaveChanges();
        _c.E.Autorizar(_edgar, MessageChannel.Sms);
        _creditoDeDora = _c.E.NuevoCredito("CR-0002", MensajesDePrueba.Norte, _dora);
        _creditoDeEdgar = _c.E.NuevoCredito("CR-0003", MensajesDePrueba.Norte, _edgar);
        foreach (var credito in new[] { _c.Credito, _creditoDeDora, _creditoDeEdgar }) _c.E.Posicion(credito, 12);
        _c.Db.SaveChanges();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private CreateMessageCampaignCommandHandler Crear() => new(_c.Db, _c.G.Actor, _c.G.Reloj);

    private PreviewMessageCampaignQueryHandler Previa() => new(_c.Db, _c.E.Cola(), _c.G.Alcance);

    private ScheduleMessageCampaignCommandHandler Programar() => new(_c.Db, _c.E.Cola(), _c.G.Alcance, _c.G.Actor, _c.G.Reloj);

    private CancelMessageCampaignCommandHandler Cancelar() => new(_c.Db, _c.E.Cola(), _c.G.Actor, _c.G.Reloj);

    private async Task<MessageCampaignDto> CampanaAsync(MessageChannel canal = MessageChannel.Sms, CampaignFilters? filtros = null,
        DateTimeOffset? sale = null)
    {
        var r = await Crear().Handle(new CreateMessageCampaignCommand("CAMP1", "Mora temprana", "MORAADMIN", canal, DebtorMessagePurpose.Collection,
            filtros ?? new CampaignFilters(MinDaysPastDue: 1), sale ?? new DateTimeOffset(2026, 12, 1, 11, 0, 0, TimeSpan.FromHours(-5)))
            { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    // ----------------------------------------------------------------------------------------------------- vista previa --

    [Fact]
    public async Task La_vista_previa_separa_a_quien_recibe_de_quien_no_autorizo_el_canal()
    {
        var campana = await CampanaAsync();

        var r = await Previa().Handle(new PreviewMessageCampaignQuery(campana.PublicId), default);

        r.Value.Recipients.Should().ContainSingle(d => d.Loan.Number == "CR-0001").Which.Decision.Should().Be("Permitido");
        r.Value.Recipients.Should().Contain(d => d.Loan.Number == "CR-0003");
        r.Value.Excluded.Should().ContainSingle(x => x.Loan.Number == "CR-0002" && x.Reason == "ChannelNotAuthorized");
        (await _c.Db.DebtorMessages.CountAsync()).Should().Be(0, "la vista previa no escribe");
    }

    [Fact]
    public async Task La_vista_previa_excluye_al_deudor_en_insolvencia_con_su_motivo()
    {
        _c.Db.InsolvencyProceedings.Add(new InsolvencyProceeding { PersonId = _edgar.Id, StartDate = _c.Hoy.AddDays(-4), Status = InsolvencyProceedingStatus.Negotiating });
        await _c.Db.SaveChangesAsync();
        var campana = await CampanaAsync();

        var r = await Previa().Handle(new PreviewMessageCampaignQuery(campana.PublicId), default);

        r.Value.Excluded.Should().Contain(x => x.Loan.Number == "CR-0003" && x.Reason == "Insolvency");
        r.Value.Recipients.Should().NotContain(d => d.Loan.Number == "CR-0003");
    }

    [Fact]
    public async Task El_filtro_de_mora_minima_deja_fuera_al_que_esta_al_dia()
    {
        var alDia = _c.E.NuevoCredito("CR-0004", MensajesDePrueba.Norte, _c.G.Persona("Fabio", "AlDia", "52333333"));
        _c.E.Posicion(alDia, 0);
        await _c.Db.SaveChangesAsync();
        var campana = await CampanaAsync(filtros: new CampaignFilters(MinDaysPastDue: 5));

        var r = await Previa().Handle(new PreviewMessageCampaignQuery(campana.PublicId), default);

        (r.Value.Recipients.Select(d => d.Loan.Number).Concat(r.Value.Excluded.Select(x => x.Loan.Number))).Should().NotContain("CR-0004");
    }

    // ----------------------------------------------------------------------------------------------------------- programar --

    [Fact]
    public async Task Programar_crea_un_mensaje_por_destinatario_con_la_campana_y_la_constancia_del_excluido()
    {
        var campana = await CampanaAsync();

        var r = await Programar().Handle(new ScheduleMessageCampaignCommand(campana.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(MessageCampaignStatus.Scheduled);
        r.Value.Counts.Should().Be(new CampaignCountsDto(2, 0, 1, 0, 0, 0));
        var mensajes = await _c.Db.DebtorMessages.AsNoTracking().Where(m => m.CampaignId != null).ToListAsync();
        mensajes.Should().HaveCount(3);
        mensajes.Select(m => m.PersonId).Should().BeEquivalentTo([_c.Ana.Id, _dora.Id, _edgar.Id]);
        mensajes.Should().OnlyContain(m => m.OriginKind == CampanasDeCobranza.Origen && m.Purpose == DebtorMessagePurpose.Collection);
        var deDora = mensajes.Single(m => m.PersonId == _dora.Id);
        var evento = await _c.Db.DebtorMessageEvents.AsNoTracking().SingleAsync(e => e.DebtorMessageId == deDora.Id);
        evento.Kind.Should().Be(DebtorMessageEventKind.Excluded);
    }

    [Fact]
    public async Task Programar_una_campana_que_ya_no_es_borrador_se_rechaza()
    {
        var campana = await CampanaAsync();
        await Programar().Handle(new ScheduleMessageCampaignCommand(campana.PublicId) { OperationKey = Guid.NewGuid() }, default);

        var otra = await Programar().Handle(new ScheduleMessageCampaignCommand(campana.PublicId) { OperationKey = Guid.NewGuid() }, default);

        otra.IsFailure.Should().BeTrue();
        (await _c.Db.DebtorMessages.CountAsync()).Should().Be(3, "no se duplican los mensajes");
    }

    [Fact]
    public async Task Un_domingo_la_campana_sale_el_lunes_diferida()
    {
        var domingo = new DateTimeOffset(2026, 12, 6, 10, 0, 0, TimeSpan.FromHours(-5));
        var campana = await CampanaAsync(sale: domingo);

        var r = await Programar().Handle(new ScheduleMessageCampaignCommand(campana.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r.Value.Counts.Deferred.Should().Be(2, "los dos autorizados salen el lunes a la primera hora permitida");
        var diferido = await _c.Db.DebtorMessageEvents.AsNoTracking().Where(e => e.Kind == DebtorMessageEventKind.Deferred).FirstAsync();
        MensajesDePrueba.Local(diferido.DueAt!.Value).DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task Una_campana_de_un_canal_sin_plantilla_no_se_crea()
    {
        var r = await Crear().Handle(new CreateMessageCampaignCommand("CAMP2", "Sin plantilla", "NOEXISTE", MessageChannel.Sms, DebtorMessagePurpose.Collection,
            new CampaignFilters(), new DateTimeOffset(2026, 12, 2, 10, 0, 0, TimeSpan.FromHours(-5))) { OperationKey = Guid.NewGuid() }, default);

        r.IsFailure.Should().BeTrue();
    }

    // ----------------------------------------------------------------------------------------------------------- cancelar --

    [Fact]
    public async Task Cancelar_deja_cancelado_lo_que_sigue_en_cola_y_conserva_la_constancia_del_excluido()
    {
        var campana = await CampanaAsync();
        await Programar().Handle(new ScheduleMessageCampaignCommand(campana.PublicId) { OperationKey = Guid.NewGuid() }, default);

        var r = await Cancelar().Handle(new CancelMessageCampaignCommand(campana.PublicId, "Se corrigió la plantilla") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(MessageCampaignStatus.Cancelled);
        r.Value.Counts.Cancelled.Should().Be(2);
        r.Value.Counts.Excluded.Should().Be(1, "la exclusión es una constancia: no se cancela");
    }

    // ------------------------------------------------------------------------------------------------------ el ciclo avanza --

    [Fact]
    public async Task El_cierre_del_ciclo_completa_la_campana_cuando_ya_no_queda_nada_en_cola()
    {
        var campana = await CampanaAsync();
        await Programar().Handle(new ScheduleMessageCampaignCommand(campana.PublicId) { OperationKey = Guid.NewGuid() }, default);
        foreach (var m in await _c.Db.DebtorMessages.AsNoTracking().Where(m => m.CampaignId != null && m.PersonId != _dora.Id).ToListAsync())
        {
            var ultimo = await _c.Db.DebtorMessageEvents.AsNoTracking().Where(e => e.DebtorMessageId == m.Id).OrderBy(e => e.Sequence).LastAsync();
            _c.Db.DebtorMessageEvents.Add(DebtorMessageEvent.Registrar(m.Id, ultimo,
                new EventoDeMensaje(DebtorMessageEventKind.Sent, _c.G.UtcNow, ActorKind.Process) { DestinationAddress = "destino", AttemptNumber = 1, ProviderReference = "REF" },
                false));
        }
        await _c.Db.SaveChangesAsync();

        await _c.Ciclo().CierreAsync(_c.Hoy, default);
        await _c.Db.SaveChangesAsync();
        await _c.Ciclo().CierreAsync(_c.Hoy, default);
        await _c.Db.SaveChangesAsync();

        (await _c.Db.MessageCampaigns.AsNoTracking().SingleAsync()).Status.Should().Be(MessageCampaignStatus.Completed);
    }
}
