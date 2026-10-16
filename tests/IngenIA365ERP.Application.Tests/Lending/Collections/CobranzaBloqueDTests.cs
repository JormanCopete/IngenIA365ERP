using FluentAssertions;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Collections.Actions;
using IngenIA365ERP.Application.Lending.Collections.Campaigns;
using IngenIA365ERP.Application.Lending.Collections.Fees;
using IngenIA365ERP.Application.Lending.Collections.Legal;
using IngenIA365ERP.Application.Lending.Collections.Stages;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// Feature 013, K4, bloque D (decisiones del dueño del 2026-10-16): (1) un proceso jurídico suspendido por insolvencia se reanuda a mano, con motivo, cuando
/// la insolvencia ya no está en negociación, y vuelve al estado que dicen sus actuaciones; (2) el crédito que se pone al día <b>sale</b> de cobranza con su
/// entrada de salida, una gestión de sistema y el cierre de su asignación, y si recae entra como episodio nuevo; (4) el tope del gasto de cobranza rechaza
/// la regla de etapa con tarifa mayor, recorta el gasto propuesto y rechaza el honorario cobrable que lo supera; (5b) la gestión sobre un crédito fuera de
/// cobranza y los criterios de campaña que no casan con nada tienen su código. (nuevo)
/// </summary>
public sealed class CobranzaBloqueDTests : IAsyncLifetime
{
    private readonly CobranzaDePrueba _c = new();
    private Person _abogada = null!;

    public async Task InitializeAsync()
    {
        _c.E.Autorizar(_c.Ana, MessageChannel.Sms);
        await _c.SembrarEtapasAsync();
        _abogada = _c.G.Persona("Laura", "Abogada", "1000999");
        await _c.Db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private async Task<CollectionStageHistoryEntry[]> HistoriaAsync() =>
        await _c.Db.CollectionStageHistory.AsNoTracking().Where(h => h.LoanId == _c.Credito.Id).OrderBy(h => h.Id).ToArrayAsync();

    private async Task ParametroAsync(string clave, string valor)
    {
        _c.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedAt = _c.G.UtcNow.AddDays(-30), CreatedBy = "test",
        });
        await _c.Db.SaveChangesAsync();
    }

    private async Task EnEtapaAsync(string codigo)
    {
        var etapa = await _c.Db.CollectionStages.AsNoTracking().SingleAsync(s => s.Code == codigo);
        _c.Db.CollectionStageHistory.Add(CollectionStageHistoryEntry.Registrar(_c.Credito.Id, etapa, _c.Hoy, codigo == "JURIDICA" ? 75 : 20,
            CollectionStageHistoryEntry.FuenteManual, null, _c.Gestor.Id, "Prueba"));
        await _c.Db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------------------------------- (2) salida de cobranza --

    [Fact]
    public async Task El_credito_que_se_pone_al_dia_sale_de_cobranza_con_gestion_de_sistema_y_cierre_del_gestor()
    {
        await _c.ReglaDeUnGestorAsync(_c.Gestora);
        await _c.CorrerAsync(_c.EnMora(15));
        (await CobranzaDeCartera.GestorVigenteAsync(_c.Db, [(_c.Credito.Id, _c.Ana.Id)], default)).Should().ContainKey(_c.Credito.Id);

        _c.E.EnLocal(new DateTime(2026, 12, 2, 10, 0, 0));
        var alDia = _c.EnMora(-20);
        alDia.Estado.AlturaDeMora.Dias.Should().Be(0, "pagó: su próxima cuota vence en veinte días y ninguna etapa preventiva le toca");

        var r = await _c.CorrerAsync(alDia);

        r.Salidas.Should().Be(1);
        r.CambiosDeEtapa.Should().Be(0);
        var historia = await HistoriaAsync();
        historia.Select(h => h.Source).Should().Equal(CollectionStageHistoryEntry.FuenteCicloDiario, CollectionStageHistoryEntry.FuenteSalida);
        var salida = historia[1];
        (salida.StageCode, salida.Reason, salida.EnteredOn, salida.PreviousEntryId).Should().Be(("ADMIN", "Salida por pago al día", _c.Hoy, historia[0].Id));

        var gestion = await _c.Db.CollectionActions.AsNoTracking().SingleAsync(a => a.LoanId == _c.Credito.Id);
        (gestion.Channel, gestion.Result, gestion.IsDirectContact, gestion.CollectorUserId, gestion.PersonId)
            .Should().Be((CollectionActionChannel.System, CollectionActionResult.LeftCollection, false, (int?)null, _c.Ana.Id));
        gestion.Notes.Should().Be("Sale de cobranza: se puso al día el 02/12/2026");

        var asignaciones = await _c.Db.CollectorAssignments.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        asignaciones.Should().HaveCount(2);
        asignaciones[1].Should().BeEquivalentTo(new { CollectorUserId = (int?)null, LoanId = (int?)_c.Credito.Id, PreviousAssignmentId = (int?)asignaciones[0].Id },
            "salir cierra la asignación del gestor");
        (await CobranzaDeCartera.EtapaVigenteAsync(_c.Db, [_c.Credito.Id], default)).Should().BeEmpty("fuera de cobranza no hay etapa vigente");
        (await CobranzaDeCartera.GestorVigenteAsync(_c.Db, [(_c.Credito.Id, _c.Ana.Id)], default)).Should().BeEmpty();

        // Repetir la tanda el mismo día no duplica nada.
        (await _c.CorrerAsync(alDia)).Salidas.Should().Be(0);
        (await HistoriaAsync()).Should().HaveCount(2);
        (await _c.Db.CollectionActions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Si_vuelve_a_caer_en_mora_entra_como_episodio_nuevo_y_se_reparte_de_nuevo()
    {
        await _c.ReglaDeUnGestorAsync(_c.Gestora);
        await _c.CorrerAsync(_c.EnMora(15));
        _c.E.EnLocal(new DateTime(2026, 12, 2, 10, 0, 0));
        await _c.CorrerAsync(_c.EnMora(-20));
        _c.E.EnLocal(new DateTime(2026, 12, 28, 10, 0, 0));

        var r = await _c.CorrerAsync(_c.EnMora(3));

        (r.CambiosDeEtapa, r.Asignaciones, r.Salidas).Should().Be((1, 1, 0));
        var historia = await HistoriaAsync();
        historia.Select(h => h.Source).Should().Equal(CollectionStageHistoryEntry.FuenteCicloDiario, CollectionStageHistoryEntry.FuenteSalida,
            CollectionStageHistoryEntry.FuenteCicloDiario);
        historia[2].StageCode.Should().Be("ADMIN");
        historia[2].PreviousEntryId.Should().Be(historia[1].Id, "el episodio nuevo enlaza la salida");
        (await CobranzaDeCartera.GestorVigenteAsync(_c.Db, [(_c.Credito.Id, _c.Ana.Id)], default))[_c.Credito.Id].CollectorUserId.Should().Be(_c.Gestora.Id);
    }

    [Fact]
    public async Task Al_dia_pero_dentro_de_la_preventiva_no_sale_sino_que_pasa_a_la_preventiva()
    {
        await _c.CorrerAsync(_c.EnMora(15));
        _c.E.EnLocal(new DateTime(2026, 12, 2, 10, 0, 0));

        var r = await _c.CorrerAsync(_c.EnMora(-3));

        (r.Salidas, r.CambiosDeEtapa).Should().Be((0, 1));
        (await HistoriaAsync()).Select(h => h.StageCode).Should().Equal("ADMIN", "PREVENTIVA");
        (await _c.Db.CollectionActions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Fuera_de_cobranza_no_se_registra_gestion_con_el_titular_y_la_de_sistema_no_se_anula()
    {
        var registrar = new RecordCollectionActionCommandHandler(_c.Db, _c.E.Cola(), _c.Parametros(), _c.G.Alcance, _c.G.Auditoria, _c.G.Actor, _c.G.Reloj);
        var llamada = new RecordCollectionActionCommand(_c.Credito.PublicId, new DateTimeOffset(2026, 12, 1, 9, 30, 0, TimeSpan.FromHours(-5)),
            CollectionActionChannel.Phone, _c.Ana.PublicId, true, CollectionActionResult.NoAnswer) { OperationKey = Guid.NewGuid() };

        var sinEtapa = await registrar.Handle(llamada, default);

        sinEtapa.IsFailure.Should().BeTrue();
        sinEtapa.Error.Code.Should().Be(ErroresDeCobranza.CodigoSinEtapa);

        await EnEtapaAsync("ADMIN");
        var enCobranza = await registrar.Handle(llamada with { OperationKey = Guid.NewGuid() }, default);
        enCobranza.IsSuccess.Should().BeTrue(enCobranza.IsFailure ? $"{enCobranza.Error.Code}: {enCobranza.Error.Message}" : string.Empty);

        var sistema = CollectionAction.DeSistema(_c.Credito.Id, _c.Ana.Id, _c.G.UtcNow, CollectionActionResult.LeftCollection, "Sale de cobranza: prueba");
        _c.Db.CollectionActions.Add(sistema);
        await _c.Db.SaveChangesAsync();
        var anular = await new VoidCollectionActionCommandHandler(_c.Db, _c.G.Alcance, _c.G.Auditoria, _c.G.Actor)
            .Handle(new VoidCollectionActionCommand(sistema.PublicId, "No aplica") { OperationKey = Guid.NewGuid() }, default);
        anular.Error.Code.Should().Be(ErroresDeCobranza.CodigoGestionDeSistema);

        var validador = new RecordCollectionActionCommandValidator();
        validador.Validate(llamada with { Channel = CollectionActionChannel.System }).IsValid.Should().BeFalse();
        validador.Validate(llamada with { ResultCode = CollectionActionResult.LeftCollection }).IsValid.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------------- (4) tope del gasto --

    [Fact]
    public async Task Con_tope_la_regla_de_etapa_con_tarifa_mayor_se_rechaza_y_la_que_cabe_se_acepta()
    {
        await ParametroAsync(ParametrosDeCartera.CobranzaTopeGastoPorcentaje, "0.05");
        var cmd = new AddCollectionStageVersionCommandHandler(_c.Db, _c.Cerrojo, _c.Parametros());
        CollectionStageInput[] Etapas(decimal tarifa) =>
        [
            new("ADMIN", "Administrativa", 1, 30, FeeRule: new CollectionFeeRuleDto("PastDueAmount", tarifa, null, "OnStageEntry")),
            new("JURIDICA", "Jurídica", 31, null, IsLegal: true),
        ];

        var encima = await cmd.Handle(new AddCollectionStageVersionCommand(Etapas(0.10m), new DateOnly(2027, 1, 1), "Tarifa alta")
            { OperationKey = Guid.NewGuid() }, default);
        encima.IsFailure.Should().BeTrue();
        encima.Error.Code.Should().Be("Lending.CollectionStage.FeeAboveCap");

        var cabe = await cmd.Handle(new AddCollectionStageVersionCommand(Etapas(0.05m), new DateOnly(2027, 1, 1), "Tarifa en el tope")
            { OperationKey = Guid.NewGuid() }, default);
        cabe.IsSuccess.Should().BeTrue(cabe.IsFailure ? $"{cabe.Error.Code}: {cabe.Error.Message}" : string.Empty);
    }

    [Fact]
    public async Task El_gasto_propuesto_se_recorta_al_menor_entre_el_porcentaje_y_el_valor_maximo()
    {
        await ParametroAsync(ParametrosDeCartera.CobranzaTopeGastoPorcentaje, "0.02");
        await ParametroAsync(ParametrosDeCartera.CobranzaTopeGastoValorMaximo, "3000");
        var credito = _c.EnMora(40);
        var vencido = CobranzaDeCartera.ValorVencido(credito.Estado);
        var esperado = Math.Min(Math.Min(decimal.Round(vencido * 0.10m, 2, MidpointRounding.AwayFromZero), 50_000m),
            Math.Min(decimal.Round(vencido * 0.02m, 2, MidpointRounding.AwayFromZero), 3_000m));

        var r = await _c.CorrerAsync(credito);

        r.GastosPropuestos.Should().Be(1);
        var gasto = (await GastosDeCobranza.GastosAsync(_c.Db, [_c.Credito.Id], false, default)).Should().ContainSingle().Subject;
        gasto.Valor.Should().Be(esperado);
        gasto.Valor.Should().BeLessThanOrEqualTo(3_000m);
    }

    [Fact]
    public async Task El_honorario_cobrable_que_supera_el_tope_se_rechaza_y_el_que_cabe_se_informa()
    {
        await ParametroAsync(ParametrosDeCartera.CobranzaTopeGastoValorMaximo, "100000");
        await EnEtapaAsync("JURIDICA");
        var abierto = await new OpenLegalProcessCommandHandler(_c.Db, _c.Numerador(), _c.Cerrojo, _c.G.Alcance, _c.G.Auditoria, _c.G.Reloj)
            .Handle(new OpenLegalProcessCommand([_c.Credito.PublicId], _abogada.PublicId, "Juzgado 3 Civil", "2026-0123", _c.Hoy) { OperationKey = Guid.NewGuid() },
                default);
        var proceso = abierto.Value.Single();
        var actuar = new RecordLegalProcessActionCommandHandler(_c.Db, _c.E.Cola(), _c.G.Alcance, _c.G.Auditoria, _c.G.Actor, _c.G.Reloj, _c.Parametros());

        var caro = await actuar.Handle(new RecordLegalProcessActionCommand(proceso.PublicId, _c.Hoy, LegalActionKind.Fee, "Honorarios", 250_000m, true)
            { OperationKey = Guid.NewGuid() }, default);
        caro.IsFailure.Should().BeTrue();
        caro.Error.Code.Should().Be(ErroresDeCobranza.CodigoGastoSobreElTope);

        var cabe = await actuar.Handle(new RecordLegalProcessActionCommand(proceso.PublicId, _c.Hoy, LegalActionKind.Fee, "Honorarios", 80_000m, true)
            { OperationKey = Guid.NewGuid() }, default);
        cabe.IsSuccess.Should().BeTrue(cabe.IsFailure ? $"{cabe.Error.Code}: {cabe.Error.Message}" : string.Empty);
    }

    // ----------------------------------------------------------------------------------------- (1) reanudar el proceso --

    [Fact]
    public async Task El_proceso_suspendido_no_se_reanuda_mientras_la_insolvencia_negocia_y_despues_vuelve_a_su_estado()
    {
        var insolvencia = new InsolvencyProceeding { PersonId = _c.Ana.Id, StartDate = _c.Hoy.AddDays(-2), Status = InsolvencyProceedingStatus.Negotiating };
        _c.Db.InsolvencyProceedings.Add(insolvencia);
        await _c.Db.SaveChangesAsync();
        await EnEtapaAsync("JURIDICA");
        var proceso = (await new OpenLegalProcessCommandHandler(_c.Db, _c.Numerador(), _c.Cerrojo, _c.G.Alcance, _c.G.Auditoria, _c.G.Reloj)
            .Handle(new OpenLegalProcessCommand([_c.Credito.PublicId], _abogada.PublicId, "Juzgado 3 Civil", null, _c.Hoy) { OperationKey = Guid.NewGuid() },
                default)).Value.Single();
        proceso.Status.Should().Be(LegalProcessStatus.Suspended);
        var reanudar = new ResumeLegalProcessCommandHandler(_c.Db, _c.G.Alcance, _c.G.Auditoria, _c.G.Reloj);
        ResumeLegalProcessCommand Orden() => new(proceso.PublicId, "La insolvencia terminó") { OperationKey = Guid.NewGuid() };

        var negociando = await reanudar.Handle(Orden(), default);
        negociando.IsFailure.Should().BeTrue();
        negociando.Error.Code.Should().Be(ErroresDeCobranza.CodigoInsolvenciaEnNegociacion);

        // La insolvencia termina (fracasa): no se reanuda sola.
        (await _c.Db.InsolvencyProceedings.SingleAsync()).Status = InsolvencyProceedingStatus.Failed;
        await _c.Db.SaveChangesAsync();
        (await _c.Db.LegalProcesses.AsNoTracking().SingleAsync()).Status.Should().Be(LegalProcessStatus.Suspended, "nunca se reanuda solo");

        var reanudado = await reanudar.Handle(Orden(), default);
        reanudado.IsSuccess.Should().BeTrue(reanudado.IsFailure ? $"{reanudado.Error.Code}: {reanudado.Error.Message}" : string.Empty);
        reanudado.Value.Status.Should().Be(LegalProcessStatus.Assigned, "sin radicación vuelve a asignado, el estado que tenía al suspenderse");
        reanudado.Value.SuspendedByInsolvencyPublicId.Should().BeNull();

        var otraVez = await reanudar.Handle(Orden(), default);
        otraVez.Error.Code.Should().Be(LegalProcess.CodigoEstadoInvalido);
        new ResumeLegalProcessCommandValidator().Validate(new ResumeLegalProcessCommand(proceso.PublicId, " ")).IsValid.Should().BeFalse("el motivo es obligatorio");
    }

    [Fact]
    public async Task El_proceso_radicado_y_en_curso_vuelve_en_curso()
    {
        await EnEtapaAsync("JURIDICA");
        var proceso = (await new OpenLegalProcessCommandHandler(_c.Db, _c.Numerador(), _c.Cerrojo, _c.G.Alcance, _c.G.Auditoria, _c.G.Reloj)
            .Handle(new OpenLegalProcessCommand([_c.Credito.PublicId], _abogada.PublicId, "Juzgado 3 Civil", null, _c.Hoy) { OperationKey = Guid.NewGuid() },
                default)).Value.Single();
        var actuar = new RecordLegalProcessActionCommandHandler(_c.Db, _c.E.Cola(), _c.G.Alcance, _c.G.Auditoria, _c.G.Actor, _c.G.Reloj, _c.Parametros());
        foreach (var clase in new[] { LegalActionKind.Filing, LegalActionKind.Order })
            (await actuar.Handle(new RecordLegalProcessActionCommand(proceso.PublicId, _c.Hoy, clase, clase.ToString()) { OperationKey = Guid.NewGuid() }, default))
                .IsSuccess.Should().BeTrue();
        var insolvencia = new InsolvencyProceeding { PersonId = _c.Ana.Id, StartDate = _c.Hoy, Status = InsolvencyProceedingStatus.Negotiating };
        _c.Db.InsolvencyProceedings.Add(insolvencia);
        await _c.Db.SaveChangesAsync();
        (await CobroJuridicoDeCartera.SuspenderPorInsolvenciaAsync(_c.Db, _c.Ana.Id, insolvencia.Id, default)).Should().Be(1);
        insolvencia.Status = InsolvencyProceedingStatus.Agreement;
        await _c.Db.SaveChangesAsync();

        var r = await new ResumeLegalProcessCommandHandler(_c.Db, _c.G.Alcance, _c.G.Auditoria, _c.G.Reloj)
            .Handle(new ResumeLegalProcessCommand(proceso.PublicId, "Acuerdo de insolvencia aprobado") { OperationKey = Guid.NewGuid() }, default);

        r.Value.Status.Should().Be(LegalProcessStatus.InProgress);
    }

    // ----------------------------------------------------------------------------------- (5b) criterios de campaña --

    [Fact]
    public async Task Los_criterios_de_campana_que_no_casan_con_nada_se_rechazan()
    {
        var crear = new CreateMessageCampaignCommandHandler(_c.Db, _c.G.Actor, _c.G.Reloj);
        var sale = new DateTimeOffset(2026, 12, 1, 11, 0, 0, TimeSpan.FromHours(-5));
        Task<Application.Common.Models.Result<MessageCampaignDto>> Crear(string codigo, CampaignFilters f) =>
            crear.Handle(new CreateMessageCampaignCommand(codigo, "Campaña", "MORAADMIN", MessageChannel.Sms, DebtorMessagePurpose.Collection, f, sale)
                { OperationKey = Guid.NewGuid() }, default);

        (await Crear("C1", new CampaignFilters(StageCode: "NOEXISTE"))).Error.Code.Should().Be(ErroresDeCobranza.CodigoCriteriosInvalidos);
        (await Crear("C2", new CampaignFilters(StageCode: "ADMIN", MinDaysPastDue: 45))).Error.Code.Should().Be(ErroresDeCobranza.CodigoCriteriosInvalidos,
            "la administrativa llega hasta 30 días");
        (await Crear("C3", new CampaignFilters(OfficePublicId: Guid.NewGuid()))).Error.Code.Should().Be(ErroresDeCobranza.CodigoCriteriosInvalidos);
        var bien = await Crear("C4", new CampaignFilters(StageCode: "ADMIN", MinDaysPastDue: 10));
        bien.IsSuccess.Should().BeTrue(bien.IsFailure ? $"{bien.Error.Code}: {bien.Error.Message}" : string.Empty);
    }
}
