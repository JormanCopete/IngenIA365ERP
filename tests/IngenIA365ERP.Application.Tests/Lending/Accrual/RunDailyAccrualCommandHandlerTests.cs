using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Accrual;

/// <summary>
/// Feature 013, US4, T0471 (DT §2.13; data-model §5, §6, §22; mensajes.md §10.0, §10.4, §10.6, §10.15; FR-060, FR-062–FR-066; T5, T6, T11, T14, T56,
/// C02, C8): una tanda del ciclo diario a la fecha d sobre el crédito de Ana (3.000.000 al 20 % EA desde el 2026-12-01). Causa corriente, mora y
/// seguros por cuota y día; repetir no escribe; un crédito atrasado se causa de a una fecha hasta d; las partes son una por fecha × sucursal × tipo
/// × tanda con su mensaje masivo; las transiciones salen por crédito; el ciclo detecta la dimensión cambiada y pone y levanta los bloqueos por mora y
/// por retiro del asociado; un crédito que falla queda en los errores y su marca no avanza; una modalidad sin usura deja un error de grupo; y lo
/// origina el proceso.
/// </summary>
public class RunDailyAccrualCommandHandlerTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;

    public async Task InitializeAsync() => _c = await CausacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    [Fact]
    public async Task Causa_el_interes_corriente_del_dia_por_cuota_con_su_parte_su_mensaje_su_marca_y_su_posicion()
    {
        var d = Hoy.AddDays(1);

        var corrida = await _c.CausarHastaAsync(d);

        corrida.Status.Should().Be(LendingProcessRunStatus.Completed);
        corrida.ProcessedLoans.Should().Be(1);
        (await _c.ControlAsync()).LastAccruedDate.Should().Be(d);
        var fila = await _c.Db.LoanTransactions.SingleAsync(t => t.Kind == LoanTransactionKind.Accrual);
        (fila.Concept, fila.InstallmentNumber, fila.ValueDate, fila.AccountingDate, fila.ProcessRunId, fila.OperationId)
            .Should().Be((LoanConcept.CurrentInterest, (int?)1, d, d, (int?)corrida.Id, (long?)null));
        fila.Amount.Should().BePositive().And.BeLessThan(2_000m, "un día de 3.000.000 al 20 % EA");

        var parte = await _c.Db.AccountingFactParts.SingleAsync();
        parte.Should().BeEquivalentTo(new
        {
            SourceKind = AccountingFactPartSource.DailyCycleBatch, ProcessRunId = (int?)corrida.Id, BatchNumber = (int?)1, FactType = InteresCausadoV1.Type,
            PerLoanMessages = false, ValueDate = d, AccountingDate = d, BranchId = _c.Credito.BranchId, MessageCount = 1, LoanCount = 1, TotalAmount = fila.Amount,
        });
        parte.LoanListSha256.Should().HaveLength(64);
        var mensaje = await _c.Db.IntegrationMessages.SingleAsync(m => m.Type == InteresCausadoV1.Type);
        mensaje.PublicId.Should().Be(parte.MessagePublicId!.Value);
        mensaje.OriginPublicId.Should().Be(parte.PublicId);
        mensaje.OriginEventKey.Should().Be($"Confirmation:{parte.PublicId:N}");
        mensaje.OriginNumber.Should().Be($"CAU-{d:yyyyMMdd}-0001", "el rótulo de la parte del ciclo (mensajes.md §3), en los 30 de la columna");
        mensaje.OriginActorKind.Should().Be(ActorKind.Process, "lo origina el proceso: el tipo lo admite (T11)");
        fila.MessagePublicId.Should().Be(mensaje.PublicId, "la fila guarda el mensaje de su parte (T14)");
        mensaje.PayloadJson.Should().Contain("\"runKind\":\"DailyCycle\"").And.Contain("\"controlTotal\"").And.Contain("\"partNumber\":1");

        var posicion = await _c.Db.LoanDailyPositions.SingleAsync(p => p.PositionDate == d);
        (posicion.Version, posicion.Principal, posicion.CurrentInterest, posicion.DaysPastDue).Should().Be((1, 3_000_000m, fila.Amount, 0));
        posicion.PayoffAmount.Should().Be(3_000_000m + fila.Amount);
    }

    [Fact]
    public async Task Repetir_la_tanda_o_la_corrida_no_escribe_nada()
    {
        var d = Hoy.AddDays(2);
        var corrida = await _c.CausarHastaAsync(d);
        var filas = await _c.Db.LoanTransactions.CountAsync();
        var mensajes = await _c.Db.IntegrationMessages.CountAsync();

        var otraTanda = await _c.Causar().Handle(new RunDailyAccrualCommand(corrida.PublicId), default);
        var otra = await _c.CorrerCicloAsync(await _c.CicloAsync(d));

        otraTanda.Value.Should().Be(AvanceDeCorrida.Terminada);
        otra.ProcessedLoans.Should().Be(0);
        (await _c.Db.LoanTransactions.CountAsync()).Should().Be(filas);
        (await _c.Db.IntegrationMessages.CountAsync()).Should().Be(mensajes);
    }

    [Fact]
    public async Task Un_credito_con_la_marca_atrasada_se_causa_de_a_una_fecha_hasta_d_con_una_tanda_por_fecha()
    {
        var d = Hoy.AddDays(3);

        var corrida = await _c.CausarHastaAsync(d);

        var filas = await _c.Db.LoanTransactions.Where(t => t.Kind == LoanTransactionKind.Accrual).OrderBy(t => t.ValueDate).ToListAsync();
        filas.Select(f => f.ValueDate).Should().Equal(Hoy.AddDays(1), Hoy.AddDays(2), Hoy.AddDays(3));
        var partes = await _c.Db.AccountingFactParts.OrderBy(p => p.ValueDate).ToListAsync();
        partes.Select(p => (p.ValueDate, p.BatchNumber)).Should().Equal((Hoy.AddDays(1), (int?)1), (Hoy.AddDays(2), (int?)2), (Hoy.AddDays(3), (int?)3));
        corrida.BatchCount.Should().BeGreaterThanOrEqualTo(3);
        (await _c.ControlAsync()).LastAccruedDate.Should().Be(d);
        (await _c.Db.LoanDailyPositions.CountAsync(p => p.PositionDate > Hoy)).Should().Be(3);
    }

    [Fact]
    public async Task La_causacion_por_dias_es_igual_al_libro_teorico_de_la_reconstruccion()
    {
        var d = new DateOnly(2027, 1, 20);

        await _c.CausarHastaAsync(d);

        var cargado = await _c.Cargador().CargarAsync(_c.Credito.Id, null, d, default);
        var libro = (await _c.Cargador().LibroAsync([_c.Credito.Id], d, default))[_c.Credito.Id];
        MotorDeCartera.Verificar(cargado!.Entrada!, d, libro).Should().BeEmpty("Σ libro = reconstrucción por día, concepto y cuota (§5.3)");
    }

    [Fact]
    public async Task Vencida_la_cuota_causa_mora_sobre_lo_vencido_topada_por_la_usura_y_lo_alerta()
    {
        await _c.FestivoAsync(new DateOnly(2027, 1, 1));
        var d = new DateOnly(2027, 1, 10);

        await _c.CausarHastaAsync(d);

        var mora = await _c.Db.LoanTransactions.Where(t => t.Concept == LoanConcept.DefaultInterest).ToListAsync();
        mora.Should().NotBeEmpty().And.OnlyContain(t => t.InstallmentNumber == 1, "la mora corre sólo sobre la cuota vencida");
        mora.Min(t => t.ValueDate).Should().BeAfter(new DateOnly(2027, 1, 2), "el 1 de enero es festivo: la cuota se exige el sábado 2 (art. 829)");
        (await _c.Db.AccountingFactParts.AnyAsync(p => p.FactType == MoraCausadaV1.Type)).Should().BeTrue();
        var posicion = await _c.Db.LoanDailyPositions.SingleAsync(p => p.PositionDate == d);
        posicion.DaysPastDue.Should().BePositive();
        posicion.UsuryCapped.Should().BeTrue("la mora de 1,5 veces el 20 % pasa la usura del 25 %");
        _c.D.O.L.Levantadas.Should().Contain(a => a.TypeCode == TiposDeAlerta.UsuraTopada && a.DedupKey == $"{TiposDeAlerta.UsuraTopada}:{_c.Credito.PublicId:D}:2027-01");
    }

    [Fact]
    public async Task Al_cruzar_el_umbral_lo_nuevo_va_a_suspenso_con_CausacionSuspendida_por_credito()
    {
        await _c.AnexoAsync(new DateOnly(2026, 1, 1));
        var d = new DateOnly(2027, 3, 15);

        await _c.CausarHastaAsync(d);

        (await _c.Db.LoanTransactions.AnyAsync(t => t.Concept == LoanConcept.CurrentInterestInSuspense)).Should().BeTrue();
        var suspendida = await _c.Db.IntegrationMessages.SingleAsync(m => m.Type == CausacionSuspendidaV1.Type);
        suspendida.OriginEventKey.Should().Be($"Confirmation:{_c.Credito.PublicId:N}", "el hecho por crédito del ciclo (C02)");
        suspendida.OriginNumber.Should().StartWith("CAU-").And.HaveLength("CAU-20270315-0001".Length);
        suspendida.PersonPublicId.Should().Be(_c.D.O.Ana.PublicId);
        suspendida.PayloadJson.Should().Contain("\"cause\":\"ArrearsThreshold\"").And.Contain("\"daysPastDue\":61");
        var parte = await _c.Db.AccountingFactParts.SingleAsync(p => p.FactType == CausacionSuspendidaV1.Type);
        (parte.PerLoanMessages, parte.MessagePublicId).Should().Be((true, (Guid?)null));
        (await _c.Db.IntegrationMessageDeliveries.SingleAsync(x => x.MessageId == suspendida.Id)).Mode.Should().Be(DeliveryMode.Always, "informativo, sin comprobante");
    }

    [Fact]
    public async Task Con_los_tipos_sembrados_la_parte_va_por_lotes_y_al_terminar_queda_el_lote_de_la_causacion()
    {
        await _c.SembrarTiposDeHechoAsync();
        var d = Hoy.AddDays(1);

        await _c.CausarHastaAsync(d);

        var mensaje = await _c.Db.IntegrationMessages.SingleAsync(m => m.Type == InteresCausadoV1.Type);
        var entrega = await _c.Db.IntegrationMessageDeliveries.SingleAsync(x => x.MessageId == mensaje.Id);
        entrega.Mode.Should().Be(DeliveryMode.Batch);
        entrega.BatchScopeKey.Should().Be(EmisionDeCartera.AlcanceDeLoteDeCausacion(d));
        entrega.ScheduleKey.Should().Contain("FinDeCausacion");
        var lote = await _c.Db.IntegrationBatches.SingleAsync();
        lote.Trigger.Should().Be(BatchTrigger.LendingAccrualRun);
        entrega.BatchId.Should().Be(lote.Id);
    }

    [Fact]
    public async Task El_lote_de_la_causacion_toma_tambien_los_alcances_anteriores_sin_lote_y_no_los_de_dias_siguientes()
    {
        // T0654, C09: el lote LendingAccrualRun del día d toma las entregas InBatch sin lote de AccrualRun:{x} con x ≤ d.
        await _c.SembrarTiposDeHechoAsync();
        var d = Hoy.AddDays(1);
        IngenIA365ERP.Domain.Entities.Integration.IntegrationMessageDelivery Pendiente(string alcance)
        {
            var mensaje = new IngenIA365ERP.Domain.Entities.Integration.Transactions.IntegrationMessage
            {
                Type = AjusteDeCausacionPorFechaValorV1.Type, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = "CAR",
                OriginKind = MessageOriginKind.Operation, OriginPublicId = Guid.NewGuid(), OriginNumber = "AJ", OriginEventKey = "Confirmation",
                ChainRootPublicId = Guid.NewGuid(), OperationDate = Hoy, BranchPublicId = Guid.NewGuid(), Currency = "COP", ExchangeRate = 1m,
                PayloadJson = "{}", PayloadSha256 = "x", OriginActorKind = ActorKind.Person, OriginUserName = "Ana", EmittedAt = DateTime.UtcNow,
            };
            var entrega = new IngenIA365ERP.Domain.Entities.Integration.IntegrationMessageDelivery
            {
                Message = mensaje, Destination = "Accounting", Mode = DeliveryMode.Batch, Status = DeliveryStatus.InBatch,
                ScheduleKey = "CAR.AFV|FinDeCausacion||PorDia", BatchScopeKey = alcance,
            };
            _c.Db.IntegrationMessages.Add(mensaje);
            _c.Db.IntegrationMessageDeliveries.Add(entrega);
            return entrega;
        }
        var deAyer = Pendiente(EmisionDeCartera.AlcanceDeLoteDeCausacion(Hoy));
        var deManana = Pendiente(EmisionDeCartera.AlcanceDeLoteDeCausacion(d.AddDays(1)));
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();

        await _c.CausarHastaAsync(d);

        var lote = await _c.Db.IntegrationBatches.SingleAsync(b => b.Trigger == BatchTrigger.LendingAccrualRun);
        (await _c.Db.IntegrationMessageDeliveries.SingleAsync(x => x.Id == deAyer.Id)).BatchId.Should().Be(lote.Id, "AccrualRun:{d-1} ≤ d");
        (await _c.Db.IntegrationMessageDeliveries.SingleAsync(x => x.Id == deManana.Id)).BatchId.Should().BeNull("AccrualRun:{d+1} es del lote siguiente");
        (await _c.Db.IntegrationMessageDeliveries.Where(x => x.BatchId == lote.Id).CountAsync()).Should().Be(lote.MessageCount);
    }

    [Fact]
    public async Task El_ciclo_detecta_que_el_deudor_dejo_de_ser_empleado_y_emite_la_dimension_cambiada_por_lotes()
    {
        await _c.SembrarTiposDeHechoAsync();
        var ana = await _c.Db.People.SingleAsync(p => p.Id == _c.D.O.Ana.Id);
        ana.IsEmployee = false;
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();

        await _c.CausarHastaAsync(Hoy.AddDays(1));

        var cambio = await _c.Db.IntegrationMessages.SingleAsync(m => m.Type == DimensionContableCambiadaV1.Type);
        cambio.PayloadJson.Should().Contain("\"changedDimensions\":[\"isEmployee\"]").And.Contain("\"isEmployee\":false");
        (await _c.Db.IntegrationMessageDeliveries.SingleAsync(x => x.MessageId == cambio.Id)).Mode.Should().Be(DeliveryMode.Batch,
            "la detecta el proceso: por lotes sin vigencia propia (ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite)");

        await _c.CausarHastaAsync(Hoy.AddDays(2));
        (await _c.Db.IntegrationMessages.CountAsync(m => m.Type == DimensionContableCambiadaV1.Type)).Should().Be(1, "ya no cambió otra vez");
    }

    [Fact]
    public async Task El_retiro_del_asociado_bloquea_solo_y_el_reingreso_lo_levanta_solo()
    {
        var ana = await _c.Db.People.SingleAsync(p => p.Id == _c.D.O.Ana.Id);
        ana.IsAssociate = false;
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();

        await _c.CausarHastaAsync(Hoy.AddDays(1));
        await _c.CausarHastaAsync(Hoy.AddDays(2));

        var bloqueo = await _c.Db.DebtorBlocks.SingleAsync(b => b.Reason == DebtorBlockReason.AssociateWithdrawal);
        (bloqueo.IsAutomatic, bloqueo.LiftedAt).Should().Be((true, (DateTime?)null));

        var lift = await new LiftDebtorBlockCommandHandler(_c.Db, _c.D.G.Actor, _c.D.G.Reloj)
            .Handle(new LiftDebtorBlockCommand(bloqueo.PublicId, "a mano") { OperationKey = Guid.NewGuid() }, default);
        lift.Error.Code.Should().Be("Lending.Block.Automatic");

        ana = await _c.Db.People.SingleAsync(p => p.Id == _c.D.O.Ana.Id);
        ana.IsAssociate = true;
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
        await _c.CausarHastaAsync(Hoy.AddDays(3));

        var levantado = await _c.Db.DebtorBlocks.SingleAsync(b => b.Reason == DebtorBlockReason.AssociateWithdrawal);
        levantado.LiftReason.Should().Be(CicloDiarioDeCartera.MotivoDeReingreso);
        levantado.LiftedOn.Should().Be(Hoy.AddDays(3));
    }

    [Fact]
    public async Task Un_credito_que_falla_queda_en_los_errores_sin_avanzar_su_marca_y_los_demas_siguen()
    {
        var otro = await _c.DesembolsarAsync(_c.D.O.Solicitud(solicitante: _c.D.O.Beto));
        _c.Db.LoanOperations.Add(new LoanOperation
        {
            LoanId = otro.Id, Kind = LoanOperationKind.Payment, Number = "PAG-1", Sequence = 99, ValueDate = Hoy, AccountingDate = Hoy,
            RegisteredAt = _c.D.G.UtcNow, Amount = 1m, ActorKind = ActorKind.Person, InputsSha256 = new string('a', 64), RulesVersion = "LND-2026.1",
        });
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();

        var corrida = await _c.CausarHastaAsync(Hoy.AddDays(1));

        corrida.Status.Should().Be(LendingProcessRunStatus.CompletedWithErrors);
        (corrida.ProcessedLoans, corrida.FailedLoans).Should().Be((1, 1));
        var error = await _c.Db.LendingProcessRunErrors.SingleAsync();
        (error.LoanId, error.Step, error.ErrorCode).Should().Be(((int?)otro.Id, "Causacion", "Lending.Replay.OperationKindNotSupported"));
        error.AlertPublicId.Should().NotBeNull();
        (await _c.ControlAsync(otro.Id)).LastAccruedDate.Should().Be(Hoy);
        (await _c.ControlAsync()).LastAccruedDate.Should().Be(Hoy.AddDays(1));
        _c.D.O.L.Levantadas.Should().Contain(a => a.TypeCode == CicloDiarioDeCartera.ErrorDeCredito && a.DedupKey == $"Cartera.CreditoSinCausar:{otro.Id}");
    }

    [Fact]
    public async Task Una_modalidad_sin_usura_del_dia_deja_un_error_de_grupo_y_el_credito_se_causa_hasta_la_vispera()
    {
        using var c = await CausacionDePrueba.CrearAsync(usuraHasta: Hoy.AddDays(2));

        var corrida = await c.CausarHastaAsync(Hoy.AddDays(5));

        (await c.ControlAsync()).LastAccruedDate.Should().Be(Hoy.AddDays(2), "causa hasta el último día con usura certificada");
        var error = await c.Db.LendingProcessRunErrors.SingleAsync();
        (error.LoanId, error.ErrorCode).Should().Be(((int?)null, "NationalValues.Missing"));
        error.DetailJson.Should().Contain("\"modality\":\"CONSUMO\"");
        c.D.O.L.Levantadas.Should().Contain(a => a.TypeCode == TiposDeAlerta.ValorNacionalFaltante);
        corrida.FailedLoans.Should().Be(0);
    }

    [Fact]
    public async Task Un_credito_ocupado_se_salta_y_se_cuenta()
    {
        _c.Ocupados.Add(_c.Credito.Id);

        var corrida = await _c.CausarHastaAsync(Hoy.AddDays(1));

        corrida.SkippedLoans.Should().Be(1);
        (await _c.ControlAsync()).LastAccruedDate.Should().Be(Hoy);
        (await _c.Db.LoanTransactions.CountAsync(t => t.Kind == LoanTransactionKind.Accrual)).Should().Be(0);
    }

    [Fact]
    public async Task La_corrida_retoma_desde_el_cursor_en_tandas_del_tamano_pedido()
    {
        var otro = await _c.DesembolsarAsync(_c.D.O.Solicitud(solicitante: _c.D.O.Beto));
        _c.Tanda.TamanoDeTanda = 1;
        var d = Hoy.AddDays(1);
        _c.Ir(d);
        var corrida = await _c.CicloAsync(d);

        var primera = await _c.Causar().Handle(new RunDailyAccrualCommand(corrida.PublicId), default);

        primera.Value.Should().Be(AvanceDeCorrida.Pendiente);
        var tras = await _c.Db.LendingProcessRuns.AsNoTracking().SingleAsync(r => r.Id == corrida.Id);
        tras.CursorLoanId.Should().Be(Math.Min(_c.Credito.Id, otro.Id));
        (await _c.ControlAsync(Math.Max(_c.Credito.Id, otro.Id))).LastAccruedDate.Should().Be(Hoy, "el segundo va en la tanda siguiente");

        var fin = await _c.CorrerCicloAsync(corrida);
        (fin.ProcessedLoans, fin.TotalLoans, fin.Status).Should().Be((2, 2, LendingProcessRunStatus.Completed));
        (await _c.ControlAsync(otro.Id)).LastAccruedDate.Should().Be(d);
        fin.TotalsJson.Should().Contain("\"loans\":2");
        fin.ResultSha256.Should().HaveLength(64);
    }

    [Fact]
    public async Task Al_terminar_verifica_una_muestra_del_libro_y_alerta_lo_descuadrado()
    {
        await _c.CausarHastaAsync(Hoy.AddDays(2));
        var real = await _c.Db.LoanTransactions.AsNoTracking().FirstAsync(t => t.Kind == LoanTransactionKind.Accrual);
        _c.Db.LoanTransactions.Add(new Domain.Entities.Lending.Transactions.LoanTransaction
        {
            LoanId = _c.Credito.Id, ProcessRunId = real.ProcessRunId, Kind = LoanTransactionKind.AccrualAdjustment, Concept = LoanConcept.CurrentInterest,
            InstallmentNumber = 1, Amount = 1m, ValueDate = real.ValueDate, AccountingDate = real.ValueDate,
        });
        await _c.Db.SaveChangesAsync();

        var corrida = await _c.CausarHastaAsync(Hoy.AddDays(3));

        corrida.CurrentStep.Should().Be(PasosDelCiclo.Verificacion);
        _c.D.O.L.Levantadas.Should().Contain(a => a.TypeCode == TiposDeAlerta.LibroDescuadrado && a.DedupKey == $"{TiposDeAlerta.LibroDescuadrado}:{_c.Credito.PublicId:D}");
    }
}
