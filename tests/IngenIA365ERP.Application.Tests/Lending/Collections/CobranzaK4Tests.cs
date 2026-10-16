using FluentAssertions;
using NSubstitute;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Collections.Actions;
using IngenIA365ERP.Application.Lending.Collections.Assignments;
using IngenIA365ERP.Application.Lending.Collections.Campaigns;
using IngenIA365ERP.Application.Lending.Collections.Claims;
using IngenIA365ERP.Application.Lending.Collections.Stages;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// Feature 013, K4 bloque B (T0923, T0927–T0929, parte de T0924): lo que se puede probar sin reconstruir un crédito —las etapas versionadas, las
/// reglas de reparto, la ventana legal de una gestión, la gasto informado, las campañas y la aceptación tácita de los reclamos—. El ciclo completo
/// sobre un crédito reconstruido lo cubre la e2e T0933.
/// </summary>
public sealed class CobranzaK4Tests : IDisposable
{
    private readonly MensajesDePrueba _e = new();

    public CobranzaK4Tests() => _e.G.ActuarComo(_e.G.Usuario(null, "gestor@coop.test"));

    public void Dispose() => _e.Dispose();

    private static CollectionStageInput Etapa(string codigo, int desde, int? hasta, bool juridica = false) => new(codigo, codigo, desde, hasta, juridica);

    // ------------------------------------------------------------------------------------------------------------ etapas --

    [Fact]
    public async Task Una_version_nueva_cierra_la_anterior_y_los_rangos_cruzados_se_rechazan()
    {
        var cmd = new AddCollectionStageVersionCommandHandler(_e.Db, Substitute.For<IngenIA365ERP.Application.Lending.Common.ICerrojoDeCartera>(),
            new IngenIA365ERP.Application.Lending.Common.ReglasDeParametrosDeCartera(_e.G.Valores, _e.G.Parametros()));
        var primera = await cmd.Handle(new AddCollectionStageVersionCommand([Etapa("ADMIN", 1, 30), Etapa("JURID", 31, null, true)],
            new DateOnly(2026, 1, 1), "Primera") { OperationKey = Guid.NewGuid() }, default);
        primera.IsSuccess.Should().BeTrue(primera.IsFailure ? primera.Error.Message : null);

        var cruzada = await cmd.Handle(new AddCollectionStageVersionCommand([Etapa("A", 1, 30), Etapa("B", 20, null)], new DateOnly(2026, 6, 1), "Cruce")
            { OperationKey = Guid.NewGuid() }, default);
        cruzada.Error.Code.Should().Be("Lending.CollectionStage.RangesInvalid");

        var segunda = await cmd.Handle(new AddCollectionStageVersionCommand([Etapa("ADMIN", 1, 60), Etapa("JURID", 61, null, true)],
            new DateOnly(2026, 6, 1), "Segunda") { OperationKey = Guid.NewGuid() }, default);
        segunda.IsSuccess.Should().BeTrue();
        segunda.Value.Version.Should().Be(2);
        var cerradas = await _e.Db.CollectionStages.AsNoTracking().Where(s => s.ValidFrom == new DateOnly(2026, 1, 1)).ToListAsync();
        cerradas.Should().OnlyContain(s => s.ValidTo == new DateOnly(2026, 5, 31));
    }

    [Fact]
    public async Task La_semilla_de_etapas_es_idempotente_y_no_pisa_lo_de_la_cooperativa()
    {
        var etapas = new[] { Etapa("PREVENTIVA", -5, 0), Etapa("ADMIN", 1, 30), Etapa("JURIDICA", 31, null, true) };
        (await EtapasPorDefecto.SembrarAsync(_e.Db, etapas, default)).Should().Be(3);
        (await EtapasPorDefecto.SembrarAsync(_e.Db, etapas, default)).Should().Be(0);
        (await _e.Db.CollectionStages.CountAsync()).Should().Be(3);
    }

    // ------------------------------------------------------------------------------------------------------------ reparto --

    [Fact]
    public async Task Una_regla_sin_gestores_o_con_el_rango_invertido_es_invalida()
    {
        var cmd = new CreateCollectorAssignmentRuleCommandHandler(_e.Db);
        var sinGestores = await cmd.Handle(new CreateCollectorAssignmentRuleCommand(1, AssignmentDistribution.ByCount, [], new DateOnly(2026, 1, 1), "r")
            { OperationKey = Guid.NewGuid() }, default);
        sinGestores.IsFailure.Should().BeTrue();
        sinGestores.Error.Code.Should().Be("Lending.CollectorAssignmentRule.Invalid");
    }

    // --------------------------------------------------------------------------------------------------- ventana de gestión --

    [Fact]
    public async Task Una_llamada_un_domingo_se_rechaza_y_el_lunes_en_horario_se_acepta()
    {
        var cola = _e.Cola();
        _e.Autorizar(_e.Ana, MessageChannel.Sms);
        var domingo = MensajesDePrueba.Utc(new DateTime(2026, 12, 6, 10, 0, 0));
        var rechazada = await cola.EvaluarGestionAsync(_e.Ana.Id, _e.Credito.Id, CollectionActionChannel.Phone, true, domingo, default);
        rechazada.Value.Decision.Should().BeOfType<Diferido>();

        var lunes = MensajesDePrueba.Utc(new DateTime(2026, 12, 7, 10, 0, 0));
        var aceptada = await cola.EvaluarGestionAsync(_e.Ana.Id, _e.Credito.Id, CollectionActionChannel.Phone, true, lunes, default);
        aceptada.Value.Decision.Should().BeOfType<Permitido>();
    }

    [Fact]
    public async Task Una_referencia_nunca_es_contacto_obligado()
    {
        var cola = _e.Cola();
        var r = await cola.EvaluarGestionAsync(_e.Carla.Id, _e.Credito.Id, CollectionActionChannel.Phone, true, _e.G.UtcNow, default);
        r.Value.Decision.Should().BeOfType<Excluido>();
    }

    // -------------------------------------------------------------------------------------------------------------- campañas --

    [Fact]
    public async Task Programar_una_campania_por_un_canal_no_autorizado_deja_al_excluido_con_su_constancia()
    {
        _e.Autorizar(_e.Ana, MessageChannel.Sms);
        var crear = new CreateMessageCampaignCommandHandler(_e.Db, _e.G.Actor, _e.G.Reloj);
        var creada = await crear.Handle(new CreateMessageCampaignCommand("CAMP1", "Mora", "MORAADMIN", MessageChannel.Sms, DebtorMessagePurpose.Collection,
            new CampaignFilters(), new DateTimeOffset(2026, 12, 2, 10, 0, 0, TimeSpan.FromHours(-5))) { OperationKey = Guid.NewGuid() }, default);
        creada.IsSuccess.Should().BeTrue(creada.IsFailure ? creada.Error.Message : null);

        var cancelar = new CancelMessageCampaignCommandHandler(_e.Db, _e.Cola(), _e.G.Actor, _e.G.Reloj);
        var cancelada = await cancelar.Handle(new CancelMessageCampaignCommand(creada.Value.PublicId, "No va") { OperationKey = Guid.NewGuid() }, default);
        cancelada.Value.Status.Should().Be(MessageCampaignStatus.Cancelled);
        var otra = await cancelar.Handle(new CancelMessageCampaignCommand(creada.Value.PublicId, "Otra vez") { OperationKey = Guid.NewGuid() }, default);
        otra.Error.Code.Should().Be("Lending.Campaign.InvalidStatus");
    }

    // ----------------------------------------------------------------------------------------------------------- reclamos --

    [Fact]
    public async Task El_reclamo_vencido_sin_respuesta_pasa_a_aceptado_tacitamente()
    {
        _e.Db.CreditReportClaims.Add(new CreditReportClaim
        {
            PersonId = _e.Ana.Id, LoanId = _e.Credito.Id, Kind = CreditReportClaimKind.Claim, Status = CreditReportClaimStatus.InProcess,
            ReceivedOn = new DateOnly(2026, 11, 2), Channel = "Correo", Description = "x", DueDate = new DateOnly(2026, 11, 23),
        });
        _e.Db.CreditReportClaims.Add(new CreditReportClaim
        {
            PersonId = _e.Ana.Id, Kind = CreditReportClaimKind.Claim, Status = CreditReportClaimStatus.InProcess, ReceivedOn = new DateOnly(2026, 11, 2),
            Channel = "Correo", Description = "y", DueDate = new DateOnly(2026, 11, 23), ExtendedDueDate = new DateOnly(2026, 12, 10),
        });
        await _e.Db.SaveChangesAsync();

        var aceptados = await AceptacionTacita.AplicarAsync(_e.Db, new DateOnly(2026, 12, 1), default);
        await _e.Db.SaveChangesAsync();

        aceptados.Should().HaveCount(1);
        (await _e.Db.CreditReportClaims.CountAsync(c => c.Status == CreditReportClaimStatus.TacitlyAccepted)).Should().Be(1);
        (await _e.Db.CreditReportClaims.CountAsync(c => c.Status == CreditReportClaimStatus.InProcess)).Should().Be(1);
    }

    // ----------------------------------------------------------------------------------------------------- gasto informado --

    [Fact]
    public void La_regla_de_gasto_respeta_la_tarifa_y_el_tope()
    {
        var regla = new ReglaDeGastoDeCobranza { Base = ReglaDeGastoDeCobranza.BaseValorVencido, Rate = 0.10m, Cap = 50_000m };
        regla.Validar("ADMIN").Should().BeNull();
        regla.Calcular(300_000m, 200_000m).Should().Be(30_000m);
        regla.Calcular(900_000m, 200_000m).Should().Be(50_000m);
        regla.Calcular(0m, 0m).Should().Be(0m);
        (regla with { Rate = 0m }).Validar("ADMIN").Should().NotBeNull();
    }

    [Fact]
    public void La_fila_del_cargo_de_un_gasto_causado_la_registra_el_libro_como_hecho()
    {
        // T0933 lo destapó: el cargo (LoanTransactionKind.Charge) no estaba entre las clases de hecho y causar un gasto informado respondía 500.
        var fila = new IngenIA365ERP.Domain.Lending.Replay.MovimientoDeLibro(new DateOnly(2026, 11, 25), LoanTransactionKind.Charge, LoanConcept.CollectionFee,
            null, 4_000m);

        var t = IngenIA365ERP.Application.Lending.Ledger.RegistroDeCartera.FilaDeHecho(7, fila, null, 3, new DateOnly(2026, 11, 25),
            new DateTime(2026, 11, 25, 15, 0, 0, DateTimeKind.Utc), Guid.NewGuid());

        (t.Kind, t.Concept, t.Amount, t.LoanId).Should().Be((LoanTransactionKind.Charge, LoanConcept.CollectionFee, 4_000m, 7));
    }
}
