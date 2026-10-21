using FluentAssertions;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Collections.Legal;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// El cobro jurídico (feature 013, K4, T0926; FR-124, api.md §14.8): sólo se abre para un crédito que ya está en la etapa jurídica; el abogado es una
/// persona del maestro que no puede ser el demandado; un solo proceso abierto por crédito; las actuaciones son inmutables y sus adjuntos del dueño
/// correcto; honorarios o gastos «a cargo del deudor» informan el gasto de cobranza con su aviso; la insolvencia en negociación suspende el proceso; se
/// cierra concluido o retirado, con motivo, y un proceso cerrado ya no admite actuaciones. (nuevo)
/// </summary>
public sealed class CobroJuridicoTests : IAsyncLifetime
{
    private readonly CobranzaDePrueba _c = new();
    private Person _abogada = null!;

    public async Task InitializeAsync()
    {
        _c.E.Autorizar(_c.Ana, MessageChannel.Sms);
        await _c.SembrarEtapasAsync();
        _abogada = _c.G.Persona("Laura", "Abogada", "1000999");
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private OpenLegalProcessCommandHandler Abrir() => new(_c.Db, _c.Numerador(), _c.Cerrojo, _c.G.Alcance, _c.G.Auditoria, _c.G.Reloj);

    private RecordLegalProcessActionCommandHandler Actuar() => new(_c.Db, _c.E.Cola(), _c.G.Alcance, _c.G.Auditoria, _c.G.Actor, _c.G.Reloj, _c.Parametros());

    private CloseLegalProcessCommandHandler Cerrar() => new(_c.Db, _c.G.Alcance, _c.G.Auditoria, _c.G.Reloj);

    private async Task EnEtapaAsync(string codigo, Loan? credito = null)
    {
        var etapa = await _c.Db.CollectionStages.AsNoTracking().SingleAsync(s => s.Code == codigo);
        var c = credito ?? _c.Credito;
        _c.Db.CollectionStageHistory.Add(CollectionStageHistoryEntry.Registrar(c.Id, etapa, _c.Hoy, codigo == "JURIDICA" ? 75 : 20,
            CollectionStageHistoryEntry.FuenteManual, null, _c.Gestor.Id, "Prueba"));
        await _c.Db.SaveChangesAsync();
    }

    private Task<Application.Common.Models.Result<IReadOnlyList<LegalProcessDto>>> AbrirAsync(Loan? credito = null, Person? abogado = null) =>
        Abrir().Handle(new OpenLegalProcessCommand([(credito ?? _c.Credito).PublicId], (abogado ?? _abogada).PublicId, "Juzgado 3 Civil", "2026-0123",
            _c.Hoy) { OperationKey = Guid.NewGuid() }, default);

    private async Task<LegalProcessDto> AbiertoAsync()
    {
        await EnEtapaAsync("JURIDICA");
        var r = await AbrirAsync();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value.Single();
    }

    // ---------------------------------------------------------------------------------------------------------- apertura --

    [Fact]
    public async Task Un_credito_que_no_esta_en_la_etapa_juridica_no_se_entrega_al_abogado()
    {
        await EnEtapaAsync("ADMIN");

        var r = await AbrirAsync();

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Lending.LegalProcess.LoanNotInLegalStage");
        (await _c.Db.LegalProcesses.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_credito_sin_historia_de_etapa_tampoco_se_entrega()
    {
        var r = await AbrirAsync();

        r.Error.Code.Should().Be("Lending.LegalProcess.LoanNotInLegalStage");
    }

    [Fact]
    public async Task En_la_etapa_juridica_se_abre_con_numero_abogado_y_estado_asignado()
    {
        var p = await AbiertoAsync();

        p.Status.Should().Be(LegalProcessStatus.Assigned);
        p.Number.Should().StartWith("JUR-").And.NotBeNullOrEmpty();
        p.Lawyer.PersonPublicId.Should().Be(_abogada.PublicId);
        p.Debtor.PersonPublicId.Should().Be(_c.Ana.PublicId);
        (p.Court, p.FileNumber, p.StartDate).Should().Be(("Juzgado 3 Civil", "2026-0123", _c.Hoy));
        p.SuspendedByInsolvencyPublicId.Should().BeNull();
        p.Loans.Should().ContainSingle(l => l.LoanPublicId == _c.Credito.PublicId);
    }

    [Fact]
    public async Task El_demandado_no_puede_ser_su_propio_abogado()
    {
        await EnEtapaAsync("JURIDICA");

        var r = await AbrirAsync(abogado: _c.Ana);

        r.IsFailure.Should().BeTrue();
        (await _c.Db.LegalProcesses.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_credito_no_tiene_dos_procesos_abiertos()
    {
        await AbiertoAsync();

        var otra = await AbrirAsync();

        otra.IsFailure.Should().BeTrue();
        (await _c.Db.LegalProcesses.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Con_insolvencia_en_negociacion_el_proceso_nace_suspendido()
    {
        _c.Db.InsolvencyProceedings.Add(new InsolvencyProceeding { PersonId = _c.Ana.Id, StartDate = _c.Hoy.AddDays(-2), Status = InsolvencyProceedingStatus.Negotiating });
        await _c.Db.SaveChangesAsync();
        await EnEtapaAsync("JURIDICA");

        var r = await AbrirAsync();

        var p = r.Value.Single();
        p.Status.Should().Be(LegalProcessStatus.Suspended);
        p.SuspendedByInsolvencyPublicId.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------------------------------------- actuaciones --

    [Fact]
    public async Task La_radicacion_pasa_a_radicado_y_la_siguiente_actuacion_a_en_curso()
    {
        var p = await AbiertoAsync();

        var radicada = await Actuar().Handle(new RecordLegalProcessActionCommand(p.PublicId, _c.Hoy, LegalActionKind.Filing, "Demanda radicada")
            { OperationKey = Guid.NewGuid() }, default);
        radicada.Value.Status.Should().Be(LegalProcessStatus.Filed);

        var auto = await Actuar().Handle(new RecordLegalProcessActionCommand(p.PublicId, _c.Hoy, LegalActionKind.Order, "Mandamiento de pago")
            { OperationKey = Guid.NewGuid() }, default);

        auto.Value.Status.Should().Be(LegalProcessStatus.InProgress);
        auto.Value.LastAction!.Kind.Should().Be(LegalActionKind.Order);
        (await _c.Db.LegalProcessActions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Un_adjunto_que_no_es_del_proceso_no_sirve_de_soporte()
    {
        var p = await AbiertoAsync();

        var r = await Actuar().Handle(new RecordLegalProcessActionCommand(p.PublicId, _c.Hoy, LegalActionKind.Order, "Auto", AttachmentPublicIds: [Guid.NewGuid()])
            { OperationKey = Guid.NewGuid() }, default);

        r.IsFailure.Should().BeTrue();
        (await _c.Db.LegalProcessActions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_honorario_a_cargo_del_deudor_informa_el_gasto_con_su_aviso_y_no_lo_causa()
    {
        var p = await AbiertoAsync();

        var r = await Actuar().Handle(new RecordLegalProcessActionCommand(p.PublicId, _c.Hoy, LegalActionKind.Fee, "Honorarios de la demanda", 300_000m,
            ChargeableToDebtor: true) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var actuacion = await _c.Db.LegalProcessActions.AsNoTracking().SingleAsync();
        (actuacion.ChargeableToDebtor, actuacion.Amount, actuacion.ChargeOperationId).Should().Be((true, 300_000m, (long?)null));
        var aviso = await _c.Db.DebtorMessages.AsNoTracking().SingleAsync(m => m.Purpose == DebtorMessagePurpose.CollectionFeeNotice);
        (aviso.OriginKind, aviso.OriginPublicId).Should().Be(("CollectionFee", actuacion.PublicId));
        (await _c.Db.LoanOperations.CountAsync()).Should().Be(0, "el gasto se causa después, con su aviso ya informado");
    }

    [Fact]
    public async Task Un_gasto_a_cargo_del_deudor_exige_un_honorario_o_gasto_con_valor()
    {
        var validador = new RecordLegalProcessActionCommandValidator();

        validador.Validate(new RecordLegalProcessActionCommand(Guid.NewGuid(), _c.Hoy, LegalActionKind.Order, "Auto", 50_000m, ChargeableToDebtor: true)).IsValid
            .Should().BeFalse("sólo un honorario o un gasto se cobra al deudor");
        validador.Validate(new RecordLegalProcessActionCommand(Guid.NewGuid(), _c.Hoy, LegalActionKind.Fee, "Honorarios", ChargeableToDebtor: true)).IsValid
            .Should().BeFalse("sin valor no hay qué cobrar");
        validador.Validate(new RecordLegalProcessActionCommand(Guid.NewGuid(), _c.Hoy, LegalActionKind.Expense, "Copias", 20_000m, ChargeableToDebtor: true)).IsValid
            .Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------------------------ cierre --

    [Fact]
    public async Task Se_cierra_concluido_con_motivo_y_ya_no_admite_actuaciones()
    {
        var p = await AbiertoAsync();

        var cerrado = await Cerrar().Handle(new CloseLegalProcessCommand(p.PublicId, LegalProcessStatus.Concluded, "Se pagó la obligación")
            { OperationKey = Guid.NewGuid() }, default);

        cerrado.IsSuccess.Should().BeTrue(cerrado.IsFailure ? $"{cerrado.Error.Code}: {cerrado.Error.Message}" : string.Empty);
        (cerrado.Value.Status, cerrado.Value.CloseReason).Should().Be((LegalProcessStatus.Concluded, "Se pagó la obligación"));
        var despues = await Actuar().Handle(new RecordLegalProcessActionCommand(p.PublicId, _c.Hoy, LegalActionKind.Other, "Tarde") { OperationKey = Guid.NewGuid() }, default);
        despues.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Un_proceso_cerrado_deja_abrir_otro_sobre_el_mismo_credito()
    {
        var p = await AbiertoAsync();
        await Cerrar().Handle(new CloseLegalProcessCommand(p.PublicId, LegalProcessStatus.Withdrawn, "Se retira la demanda") { OperationKey = Guid.NewGuid() }, default);

        var otra = await AbrirAsync();

        otra.IsSuccess.Should().BeTrue(otra.IsFailure ? otra.Error.Message : string.Empty);
        (await _c.Db.LegalProcesses.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task La_lista_filtra_por_estado_y_por_abogado()
    {
        await AbiertoAsync();
        var lista = new ListLegalProcessesQueryHandler(_c.Db, _c.G.Alcance);

        var del = await lista.Handle(new ListLegalProcessesQuery(Lawyer: _abogada.PublicId), default);
        var otro = await lista.Handle(new ListLegalProcessesQuery(Lawyer: _c.Beto.PublicId), default);
        var concluidos = await lista.Handle(new ListLegalProcessesQuery(Status: LegalProcessStatus.Concluded), default);

        (del.Value.Items.Count, otro.Value.Items.Count, concluidos.Value.Items.Count).Should().Be((1, 0, 0));
    }
}
