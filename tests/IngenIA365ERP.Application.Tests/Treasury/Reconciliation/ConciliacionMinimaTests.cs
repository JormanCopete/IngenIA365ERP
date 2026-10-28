using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Checkbooks;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Entities.Treasury.Remittances;
using IngenIA365ERP.Domain.Entities.Treasury.Statements;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Reconciliation;

/// <summary>
/// Feature 015, US25, T0273 (FR-131, FR-209, FR-216, FR-217): la conciliación mínima por cuenta y mes. Abrir (una abierta se devuelve, una cerrada se reabre por su ruta);
/// los registros de Recaudo por <see cref="IRecaudoParaConciliacion"/>, las remesas y los movimientos contables que no son de Recaudo ni de Tesorería; el cruce automático
/// por vínculo y por valor y fecha con holgura, con la remesa cruzada que pasa a <c>Confirmed</c> con su hecho; el cruce manual exacto, sin cruzar dos veces y deshecho en
/// una abierta; el abono sin registro que pasa a Recaudo como partida tras sus cerrojos (CH16); el cargo no identificado que alerta; el cierre con diferencia cero y su
/// informe; la reapertura con motivo como versión nueva; y el conciliador que preparó pagos de la cuenta en el mes, rechazado. Prueba escrita, sin correr.
/// </summary>
public class ConciliacionMinimaTests
{
    private const int Anio = 2026;
    private const int Mes = 11;

    private readonly EscenarioDeTesoreria _t = new();
    private readonly IRecaudoParaConciliacion _recaudo = Substitute.For<IRecaudoParaConciliacion>();
    private readonly IPartidasDesdeConciliacion _partidas = Substitute.For<IPartidasDesdeConciliacion>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDocumentosDeTesoreriaEnPdf _pdf = Substitute.For<IDocumentosDeTesoreriaEnPdf>();
    private readonly List<BankStatementLine> _lineas = [];

    private AuditoriaDeTesoreria Auditoria => new(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);

    public ConciliacionMinimaTests()
    {
        _t.E.Parametro(ParametrosDeRecaudoYTesoreria.ConciliacionDiasDeHolgura, "3", ParametrosDeRecaudoYTesoreria.ModuloTesoreria);
        _t.Contabilidad.SaldoEnLibrosAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(1_000_000m);
        _t.Contabilidad.MovimientosDeCuentaBancariaAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<MovimientoContableDeCuentaDto>());
        _t.Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<CombinacionDeHecho>>(), Arg.Any<CancellationToken>()).Returns(new EvaluacionContableDto(true, "Bloquear", []));
        _recaudo.RegistrosAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new List<RegistroConciliableDto>());
        _sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(Guid.NewGuid()));
        _pdf.Conciliacion(Arg.Any<ConciliacionModel>()).Returns([1, 2, 3]);
        _t.E.Como(_t.E.OtroCajero);
    }

    private BankStatementLine Linea(DateOnly fecha, StatementLineDirection sentido, decimal valor, string? referencia = null, decimal? saldo = null)
    {
        var extracto = _t.E.Db.TreasuryBankStatements.Local.FirstOrDefault();
        if (extracto is null)
        {
            extracto = new BankStatement
            {
                Number = "EX-1", BankAccountId = _t.Pagos.Id, Source = BankStatementSource.Manual, From = new DateOnly(Anio, Mes, 1), To = new DateOnly(Anio, Mes, 30),
                LoadedByUserId = _t.E.Supervisor.Id, LoadedAt = DateTime.UtcNow,
            };
            _t.E.Db.TreasuryBankStatements.Add(extracto);
            _t.E.Db.SaveChanges();
        }
        var l = new BankStatementLine
        {
            BankStatementId = extracto.Id, BankAccountId = _t.Pagos.Id, LineNumber = _lineas.Count + 1, MovementDate = fecha, Direction = sentido, Amount = valor,
            Reference = referencia, Balance = saldo, Fingerprint = Guid.NewGuid().ToString("N"),
            RecaudoStatus = sentido == StatementLineDirection.Credit ? StatementLineRecaudoStatus.Pending : StatementLineRecaudoStatus.NotApplicable,
        };
        _t.E.Db.TreasuryBankStatementLines.Add(l);
        _t.E.Db.SaveChanges();
        _lineas.Add(l);
        return l;
    }

    private CashRemittance Remesa(decimal valor, DateOnly salio)
    {
        var r = new CashRemittance
        {
            Number = "RM-1", Direction = RemittanceDirection.VaultToBank, BranchId = _t.E.OficinaA.Id, VaultPublicId = Guid.NewGuid(), BankAccountId = _t.Pagos.Id,
            Amount = valor, IdempotencyKey = Guid.NewGuid(), DispatchDate = salio, DispatchedAt = DateTime.UtcNow, Status = RemittanceState.InTransit,
        };
        _t.E.Db.CashRemittances.Add(r);
        _t.E.Db.SaveChanges();
        return r;
    }

    private OpenReconciliationCommandHandler Abrir() =>
        new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Segregacion, _t.Contabilidad, Auditoria, _t.E.Reloj);

    private AutoMatchReconciliationCommandHandler CruzarSolo() =>
        new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Segregacion, [_recaudo], _t.Contabilidad, _t.Parametros, _t.Emision, Auditoria, _t.E.Reloj);

    private AddManualMatchCommandHandler CruzarAMano() =>
        new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Segregacion, [_recaudo], _t.Contabilidad, _t.Parametros, _t.Emision, Auditoria, _t.E.Reloj);

    private AddReconcilingItemCommandHandler Partida() =>
        new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Segregacion, [_partidas], _t.Contabilidad, _t.E.Alertas, Auditoria, _t.E.Reloj);

    private CloseReconciliationCommandHandler Cerrar() =>
        new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Segregacion, _t.Contabilidad, _pdf, _sender, Auditoria, _t.E.Reloj);

    private async Task<ReconciliationDto> AbiertaAsync(decimal? saldo = null) =>
        (await Abrir().Handle(new OpenReconciliationCommand(_t.Pagos.PublicId, Anio, Mes, saldo) { OperationKey = Guid.NewGuid() }, default)).Value;

    [Fact]
    public async Task Abrir_toma_el_saldo_del_extracto_y_el_de_libros_y_una_abierta_se_devuelve()
    {
        Linea(new DateOnly(Anio, Mes, 28), StatementLineDirection.Credit, 50_000m, saldo: 1_050_000m);

        var a = await AbiertaAsync();
        var b = await AbiertaAsync();

        a.Status.Should().Be("Open");
        a.Version.Should().Be(1);
        a.StatementBalance.Should().Be(1_050_000m, "la última línea del mes con saldo");
        a.LedgerBalance.Should().Be(1_000_000m, "el saldo en libros lo responde Contabilidad (E-04)");
        a.Difference.Should().Be(50_000m);
        b.PublicId.Should().Be(a.PublicId);
        (await _t.E.Db.TreasuryReconciliations.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// Segunda ronda del 2026-10-27 (D-l, T0498; FR-233): la segregación se exige sólo al aprobar. Quien preparó pagos de la cuenta ya puede abrir y trabajar la
    /// conciliación; la cerrada la aprueba otra persona (el conciliador no), y cerrada sin aprobar es un pendiente del cierre del mes.
    /// </summary>
    [Fact]
    public async Task Quien_preparo_pagos_concilia_y_la_aprobacion_es_de_otra_persona()
    {
        _t.E.Db.PaymentBatches.Add(new PaymentBatch
        {
            Number = "LP-1", BankAccountId = _t.Pagos.Id, Form = PaymentBatchForm.File, PreparedByUserId = _t.E.Supervisor.Id, PreparedAt = DateTime.UtcNow,
            PayDate = new DateOnly(Anio, Mes, 10),
        });
        _t.E.Db.SaveChanges();

        var r = await Abrir().Handle(new OpenReconciliationCommand(_t.Pagos.PublicId, Anio, Mes) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);

        var conciliacion = await _t.E.Db.TreasuryReconciliations.SingleAsync();
        conciliacion.Status = ReconciliationState.Closed;
        conciliacion.ClosedByUserId = conciliacion.ConciliatorUserId;
        _t.E.Db.SaveChanges();
        (await new IngenIA365ERP.Application.Treasury.Activation.EstadoOperativoDeTesoreriaParaElCierre(_t.E.Db).PendientesAsync(Anio, Mes, default))
            .Should().Contain(p => p.Code == IngenIA365ERP.Application.Treasury.Activation.EstadoOperativoDeTesoreriaParaElCierre.CodigoConciliacionesSinAprobar);
        var aprobar = new ApproveReconciliationCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, new IngenIA365ERP.Application.Common.Security.PersonasDeLosUsuarios(_t.E.Db),
            new AuditoriaDeTesoreria(_t.E.Servicios, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditoriaDeTesoreria>.Instance), _t.E.Reloj);
        var propia = await aprobar.Handle(new ApproveReconciliationCommand(conciliacion.PublicId) { OperationKey = Guid.NewGuid() }, default);
        propia.Error.Code.Should().Be(ApproveReconciliationCommandHandler.CodigoAutoaprobada);

        _t.E.Como(_t.E.Cajera);
        var aprobada = await aprobar.Handle(new ApproveReconciliationCommand(conciliacion.PublicId) { OperationKey = Guid.NewGuid() }, default);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? aprobada.Error.Code : null);
        aprobada.Value.ApprovedAt.Should().NotBeNull();
        (await new IngenIA365ERP.Application.Treasury.Activation.EstadoOperativoDeTesoreriaParaElCierre(_t.E.Db).PendientesAsync(Anio, Mes, default))
            .Should().NotContain(p => p.Code == IngenIA365ERP.Application.Treasury.Activation.EstadoOperativoDeTesoreriaParaElCierre.CodigoConciliacionesSinAprobar);
    }

    [Fact]
    public async Task El_cruce_automatico_usa_el_vinculo_y_el_valor_y_confirma_la_remesa_cruzada()
    {
        var abono = Linea(new DateOnly(Anio, Mes, 3), StatementLineDirection.Credit, 120_000m);
        var deposito = Linea(new DateOnly(Anio, Mes, 6), StatementLineDirection.Credit, 300_000m);
        var heredado = Linea(new DateOnly(Anio, Mes, 9), StatementLineDirection.Debit, 15_000m);
        Linea(new DateOnly(Anio, Mes, 20), StatementLineDirection.Debit, 9_999m);
        var recibo = Guid.NewGuid();
        _recaudo.RegistrosAsync(_t.Pagos.PublicId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<RegistroConciliableDto> { new(recibo, "Receipt", "RB-1", new DateOnly(Anio, Mes, 3), 120_000m, "RB-1", abono.PublicId) });
        var remesa = Remesa(300_000m, new DateOnly(Anio, Mes, 5));
        var asiento = Guid.NewGuid();
        _t.Contabilidad.MovimientosDeCuentaBancariaAsync(_t.Pagos.PublicId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<MovimientoContableDeCuentaDto>
            {
                new(asiento, "CE-7", new DateOnly(Anio, Mes, 9), 0m, 15_000m, "Comisión digitada", "ACC"),
                new(Guid.NewGuid(), "RB-1", new DateOnly(Anio, Mes, 3), 120_000m, 0m, "Recibo de banco", "REC"),
            });
        var c = await AbiertaAsync();

        var r = await CruzarSolo().Handle(new AutoMatchReconciliationCommand(c.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue();
        var cruces = await _t.E.Db.TreasuryReconciliationMatches.Where(m => m.IsLive).ToListAsync();
        cruces.Should().HaveCount(3, "el movimiento contable de Recaudo no se cruza dos veces: ya está el recibo");
        cruces.Single(m => m.StatementLineId == abono.Id).Rule.Should().Be("StatementLink");
        cruces.Single(m => m.StatementLineId == deposito.Id).RecordKind.Should().Be(ReconciliationRecordKind.Remittance);
        cruces.Single(m => m.StatementLineId == heredado.Id).RecordPublicId.Should().Be(asiento);
        (await _t.E.Db.CashRemittances.SingleAsync(x => x.Id == remesa.Id)).Status.Should().Be(RemittanceState.Confirmed);
        (await _t.E.Db.IntegrationMessages.AnyAsync(m => m.Type == RemesaConfirmadaV1.Type)).Should().BeTrue();
        (await _t.E.Db.TreasuryBankStatementLines.SingleAsync(l => l.Id == abono.Id)).ReconciliationStatus.Should().Be(StatementLineReconciliationStatus.Matched);
    }

    [Fact]
    public async Task El_cruce_manual_es_exacto_no_cruza_dos_veces_y_se_deshace_en_una_abierta()
    {
        var linea = Linea(new DateOnly(Anio, Mes, 12), StatementLineDirection.Credit, 200_000m);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _recaudo.RegistrosAsync(_t.Pagos.PublicId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<RegistroConciliableDto>
            {
                new(a, "Receipt", "RB-2", new DateOnly(Anio, Mes, 2), 150_000m, "X"),
                new(b, "Receipt", "RB-3", new DateOnly(Anio, Mes, 25), 50_000m, "Y"),
            });
        var c = await AbiertaAsync();
        var mano = CruzarAMano();

        var mal = await mano.Handle(new AddManualMatchCommand(c.PublicId, linea.PublicId, [new(ReconciliationRecordKind.ReceiptsBankReceipt, a)]) { OperationKey = Guid.NewGuid() },
            default);
        mal.Error.Code.Should().Be(ErroresDeTesoreria.CodigoConciliacionValorNoCuadra);

        var bien = await mano.Handle(new AddManualMatchCommand(c.PublicId, linea.PublicId,
            [new(ReconciliationRecordKind.ReceiptsBankReceipt, a), new(ReconciliationRecordKind.ReceiptsBankReceipt, b)]) { OperationKey = Guid.NewGuid() }, default);
        bien.IsSuccess.Should().BeTrue("uno a varios a mano");

        var otra = await mano.Handle(new AddManualMatchCommand(c.PublicId, linea.PublicId, [new(ReconciliationRecordKind.ReceiptsBankReceipt, a)]) { OperationKey = Guid.NewGuid() },
            default);
        otra.Error.Code.Should().Be(ErroresDeTesoreria.CodigoConciliacionYaCruzada).And.Be("Treasury.Reconciliation.AlreadyMatched");

        var cruce = await _t.E.Db.TreasuryReconciliationMatches.FirstAsync();
        var deshecho = await new RemoveMatchCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Contabilidad, Auditoria, _t.E.Reloj)
            .Handle(new RemoveMatchCommand(c.PublicId, cruce.PublicId) { OperationKey = Guid.NewGuid() }, default);
        deshecho.IsSuccess.Should().BeTrue();
        (await _t.E.Db.TreasuryReconciliationMatches.CountAsync(m => m.IsLive)).Should().Be(0, "se deshace todo el grupo y las filas quedan, no vigentes");
        (await _t.E.Db.TreasuryReconciliationMatches.CountAsync()).Should().Be(2);
        (await _t.E.Db.TreasuryBankStatementLines.SingleAsync(l => l.Id == linea.Id)).ReconciliationStatus.Should().Be(StatementLineReconciliationStatus.Open);
    }

    [Fact]
    public async Task El_abono_sin_registro_pasa_a_Recaudo_como_partida_despues_de_sus_cerrojos()
    {
        var linea = Linea(new DateOnly(Anio, Mes, 14), StatementLineDirection.Credit, 75_000m);
        var partida = Guid.NewGuid();
        _partidas.CrearAsync(linea.PublicId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new PartidaCreadaDto(partida, "PI-9"));
        var c = await AbiertaAsync();
        _t.Cerrojo.ClearReceivedCalls();

        var r = await Partida().Handle(new AddReconcilingItemCommand(c.PublicId, ReconcilingItemKind.UnidentifiedCredit, 75_000m, StatementLinePublicId: linea.PublicId)
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _partidas.TomarCerrojosAsync(_t.Pagos.PublicId, Arg.Any<CancellationToken>());
            _t.Cerrojo.TomarSetupAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
        });
        var item = await _t.E.Db.TreasuryReconcilingItems.SingleAsync();
        item.UnidentifiedItemPublicId.Should().Be(partida);
        (await _t.E.Db.TreasuryReconciliationMatches.SingleAsync()).RecordPublicId.Should().Be(partida, "la línea queda cruzada con la partida de Recaudo");
        ConciliacionesDeTesoreria.Signo(item.Kind, item.UnidentifiedItemPublicId).Should().Be(0m, "ya está en los libros por su hecho");
    }

    [Fact]
    public async Task El_cargo_no_identificado_alerta_y_explica_la_diferencia()
    {
        var linea = Linea(new DateOnly(Anio, Mes, 15), StatementLineDirection.Debit, 40_000m);
        var c = await AbiertaAsync(960_000m);

        var r = await Partida().Handle(new AddReconcilingItemCommand(c.PublicId, ReconcilingItemKind.UnidentifiedDebit, 40_000m, StatementLinePublicId: linea.PublicId)
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.Value.Difference.Should().Be(0m, "extracto 960.000 − libros 1.000.000 + cargo no identificado 40.000");
        await _t.E.Alertas.Received(1).LevantarAsync(Arg.Is<Application.Common.Alerts.AlertaALevantar>(a => a.TypeCode == "Tesoreria.CargoNoIdentificado"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cerrar_exige_diferencia_cero_y_deja_el_informe_y_reabrir_crea_una_version_nueva()
    {
        var c = await AbiertaAsync(1_100_000m);

        var noCuadra = await Cerrar().Handle(new CloseReconciliationCommand(c.PublicId) { OperationKey = Guid.NewGuid() }, default);
        noCuadra.Error.Code.Should().Be(ErroresDeTesoreria.CodigoConciliacionNoCuadra).And.Be("Treasury.Reconciliation.NotSquared");

        await Partida().Handle(new AddReconcilingItemCommand(c.PublicId, ReconcilingItemKind.IssuedCheckNotCashed, 100_000m, new DateOnly(Anio, Mes, 27))
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        var cerrada = await Cerrar().Handle(new CloseReconciliationCommand(c.PublicId) { OperationKey = Guid.NewGuid() }, default);

        cerrada.Value.Status.Should().Be("Closed");
        cerrada.Value.ReportAttachmentPublicId.Should().NotBeNull();
        _pdf.Received(1).Conciliacion(Arg.Is<ConciliacionModel>(m => m.Items.Count == 1 && m.Difference == 0m));

        var sinPermisoDeCambio = await Partida().Handle(new AddReconcilingItemCommand(c.PublicId, ReconcilingItemKind.DepositInTransit, 1m) { OperationKey = Guid.NewGuid() },
            default);
        sinPermisoDeCambio.Error.Code.Should().Be(ErroresDeTesoreria.CodigoConciliacionNoAbierta);

        var reabierta = await new ReopenReconciliationCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Contabilidad, Auditoria, _t.E.Reloj)
            .Handle(new ReopenReconciliationCommand(c.PublicId, "El banco corrigió el extracto") { OperationKey = Guid.NewGuid() }, default);

        reabierta.Value.Version.Should().Be(2);
        reabierta.Value.Status.Should().Be("Open");
        (await _t.E.Db.TreasuryReconciliations.SingleAsync(x => x.PublicId == c.PublicId)).Status.Should().Be(ReconciliationState.Reopened);
        (await _t.E.Db.TreasuryReconcilingItems.CountAsync(i => !i.IsVoided)).Should().Be(2, "la versión nueva copia sus partidas vigentes");
        reabierta.Value.Difference.Should().Be(0m);
    }
}
