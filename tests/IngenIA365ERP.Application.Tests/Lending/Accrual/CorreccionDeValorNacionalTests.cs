using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Application.Tests.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Accrual;

/// <summary>
/// Feature 013, US4, T0476 (T28, C03, C05; mensajes.md §10.5): la réplica que trae una usura que <b>corrige</b> otra y cubre días ya causados encola, en
/// su transacción y sólo con Cartera activada, una corrida <c>NationalValueCorrection</c> con los días que toca; la corrida registra por tandas cada
/// diferencia por (día valor, concepto, cuota) como <c>AccrualAdjustment</c> de la corrida, con sus partes <c>NationalValueCorrectionBatch</c> que emiten
/// <c>AjusteDeCausacionPorFechaValor</c> (<c>cause = NationalValueCorrection</c>, el valor corregido); después el libro cuadra con la reconstrucción, y
/// una liquidación con R anterior a la corrección no cambia. El crédito de Ana (20 % EA) se causa diez días con la usura de 25 %, y la usura se corrige
/// a 18 % desde el primer día: la corriente queda topada.
/// </summary>
public class CorreccionDeValorNacionalTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;
    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;
    private static readonly DateOnly Causado = Hoy.AddDays(10);

    public async Task InitializeAsync()
    {
        _c = await CausacionDePrueba.CrearAsync();
        await _c.CausarHastaAsync(Causado);
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private async Task<NationalValue> CorreccionAsync(decimal valor = 0.18m, DateOnly? desde = null)
    {
        var original = await _c.Db.NationalValues.AsNoTracking().SingleAsync(v => v.Code == CodigosDeValorNacional.Usura && v.Key == LineasDePrueba.ClaveConsumo);
        var copia = CopiasDePrueba.Copia(CodigosDeValorNacional.Usura, LineasDePrueba.ClaveConsumo, desde ?? Hoy, valor, _c.D.G.UtcNow,
            corrige: original.SourcePublicId, unidad: "EA");
        await CopiasDePrueba.SembrarAsync(_c.Db, _c.D.G.UtcNow, copia);
        return copia;
    }

    private async Task<EfectoAplicado> ReplicarAsync(params NationalValue[] valores)
    {
        var efecto = await new EfectoDeReplicacionCorreccionDeValorNacional().AlReplicarAsync(_c.Db,
            new LoteReplicado(valores, [], _c.D.G.UtcNow, _c.D.G.HoyLocal), default);
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
        return efecto;
    }

    [Fact]
    public async Task La_replica_encola_la_correccion_solo_si_corrige_dias_ya_causados_con_Cartera_activada()
    {
        var nueva = CopiasDePrueba.Copia(CodigosDeValorNacional.Usura, LineasDePrueba.ClaveConsumo, Hoy.AddMonths(1), 0.26m, _c.D.G.UtcNow, unidad: "EA");
        (await ReplicarAsync(nueva)).Should().BeSameAs(EfectoAplicado.Nada, "no corrige nada: es una usura nueva");
        var futura = CopiasDePrueba.Copia(CodigosDeValorNacional.Usura, LineasDePrueba.ClaveConsumo, Causado.AddDays(1), 0.2m, _c.D.G.UtcNow,
            corrige: Guid.NewGuid(), unidad: "EA");
        (await ReplicarAsync(futura)).Should().BeSameAs(EfectoAplicado.Nada, "no cubre días ya causados");

        var correccion = await CorreccionAsync();
        var efecto = await ReplicarAsync(correccion);

        efecto.Conteos[EfectoDeReplicacionCorreccionDeValorNacional.ConteoCorridas].Should().Be(1);
        var corrida = await _c.Db.LendingProcessRuns.SingleAsync(r => r.Kind == LendingProcessKind.NationalValueCorrection);
        corrida.Should().BeEquivalentTo(new
        {
            Status = LendingProcessRunStatus.Requested, AffectedFrom = (DateOnly?)Hoy, AffectedTo = (DateOnly?)Causado, RequestedByKind = ActorKind.Process,
            ExecutionOrigin = $"Replica:{correccion.SourcePublicId:D}",
        });

        var setup = await _c.Db.LendingSetups.SingleAsync();
        setup.ActivationDate = null;
        await _c.Db.SaveChangesAsync();
        (await ReplicarAsync(correccion)).Should().BeSameAs(EfectoAplicado.Nada, "sin Cartera activada no se encola nada");
    }

    [Fact]
    public async Task La_corrida_registra_el_ajuste_por_dia_concepto_y_cuota_con_sus_partes_y_el_libro_vuelve_a_cuadrar()
    {
        await _c.SembrarTiposDeHechoAsync();
        var antes = _c.D.G.UtcNow.AddMinutes(-1);
        var liquidacionAntes = await _c.Liquidar().Handle(new GetLoanSettlementQuery(_c.Credito.PublicId, Causado, antes), default);
        _c.Ir(Causado.AddDays(1));
        var correccion = await CorreccionAsync();
        await ReplicarAsync(correccion);
        var corrida = await _c.Db.LendingProcessRuns.SingleAsync(r => r.Kind == LendingProcessKind.NationalValueCorrection);

        for (var i = 0; i < 10; i++)
        {
            var r = await _c.Corregir().Handle(new RunNationalValueCorrectionCommand(corrida.PublicId), default);
            r.IsSuccess.Should().BeTrue();
            if (r.Value == AvanceDeCorrida.Terminada) break;
        }
        _c.Db.ChangeTracker.Clear();

        var leida = await _c.Db.LendingProcessRuns.SingleAsync(r => r.Id == corrida.Id);
        (leida.Status, leida.ProcessedLoans, leida.NationalValueReplicationId).Should().Be((LendingProcessRunStatus.Completed, 1, (int?)correccion.ReplicationId));
        var ajustes = await _c.Db.LoanTransactions.Where(t => t.ProcessRunId == corrida.Id).ToListAsync();
        ajustes.Should().HaveCount(10, "un ajuste por cada día causado").And.OnlyContain(t => t.Kind == LoanTransactionKind.AccrualAdjustment
            && t.Concept == LoanConcept.CurrentInterest && t.Amount < 0m && t.InstallmentNumber == 1 && t.AccountingDate == Causado.AddDays(1));
        ajustes.Select(t => t.ValueDate).Should().BeEquivalentTo(Enumerable.Range(1, 10).Select(i => Hoy.AddDays(i)));

        var parte = await _c.Db.AccountingFactParts.SingleAsync(p => p.SourceKind == AccountingFactPartSource.NationalValueCorrectionBatch);
        (parte.FactType, parte.ProcessRunId, parte.TotalAmount).Should().Be((AjusteDeCausacionPorFechaValorV1.Type, (int?)corrida.Id, ajustes.Sum(a => a.Amount)));
        ajustes.Should().OnlyContain(a => a.MessagePublicId == parte.MessagePublicId);
        var mensaje = await _c.Db.IntegrationMessages.SingleAsync(m => m.PublicId == parte.MessagePublicId);
        mensaje.OriginEventKey.Should().Be($"Confirmation:{parte.PublicId:N}");
        mensaje.OriginNumber.Should().StartWith("VNC-").And.Match(n => n.Length <= PartesDeHechosDeCartera.LargoDelRotulo,
            "con el nombre del tipo («AjusteDeCausacionPorFechaValor-…») no cabía en los 30 de la columna y los dos motores rechazaban la tanda");
        mensaje.PayloadJson.Should().Contain("\"cause\":\"NationalValueCorrection\"").And.Contain("\"correctedNationalValue\":{")
            .And.Contain(correccion.SourcePublicId.ToString()).And.Contain("\"runKind\":\"NationalValueCorrection\"");
        (await _c.Db.IntegrationMessageDeliveries.SingleAsync(x => x.MessageId == mensaje.Id)).Mode.Should().Be(DeliveryMode.Batch);

        var cargado = await _c.Cargador().CargarAsync(_c.Credito.Id, null, Causado, default);
        var libro = (await _c.Cargador().LibroAsync([_c.Credito.Id], Causado, default))[_c.Credito.Id];
        MotorDeCartera.Verificar(cargado!.Entrada!, Causado, libro).Should().BeEmpty("el libro vuelve a cuadrar con la reconstrucción de ahora");
        (await _c.Db.LoanDailyPositions.CountAsync(p => p.Version == 2)).Should().Be(10, "cada día cambiado deja una versión nueva de su posición");

        var liquidacionDespues = await _c.Liquidar().Handle(new GetLoanSettlementQuery(_c.Credito.PublicId, Causado, antes), default);
        liquidacionDespues.Value.Fingerprint.Should().Be(liquidacionAntes.Value.Fingerprint, "una liquidación con R anterior a la corrección no cambia (SC-002)");
        var ahora = await _c.Liquidar().Handle(new GetLoanSettlementQuery(_c.Credito.PublicId, Causado), default);
        ahora.Value.ByConcept["CurrentInterest"].Should().BeLessThan(liquidacionAntes.Value.ByConcept["CurrentInterest"]);
    }
    // ------------------------------------------------------------------------------------- K6b: la corrección de una UVR (fase 24) --

    /// <summary>Una UVR de <paramref name="dia"/> que corrige la publicada (como la serie diaria: clave vacía, vigente sólo ese día).</summary>
    private NationalValue CorreccionDeUvr(DateOnly dia, decimal valor = 407.9m) =>
        CopiasDePrueba.Copia(CodigosDeValorNacional.Uvr, string.Empty, dia, valor, _c.D.G.UtcNow, hasta: dia, corrige: Guid.NewGuid(), unidad: "Pesos");

    /// <summary>Sella el crédito de Ana como crédito en UVR (la unidad va en sus condiciones; aquí sólo importa para elegir a los afectados).</summary>
    private async Task CreditoEnUvrAsync()
    {
        foreach (var t in await _c.Db.LoanTermsVersions.Where(t => t.LoanId == _c.Credito.Id).ToListAsync())
            _c.Db.Entry(t).Property(x => x.DenominationUnit).CurrentValue = DenominationUnit.Uvr;
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task La_correccion_de_una_UVR_ya_replicada_encola_la_corrida_solo_si_hay_creditos_en_UVR_causados_ese_dia()
    {
        (await ReplicarAsync(CorreccionDeUvr(Hoy.AddDays(3)))).Should().BeSameAs(EfectoAplicado.Nada,
            "el crédito de Ana es en pesos: la UVR no lo toca");

        await CreditoEnUvrAsync();
        (await ReplicarAsync(CorreccionDeUvr(Causado.AddDays(1)))).Should().BeSameAs(EfectoAplicado.Nada, "el día corregido no está causado");
        (await ReplicarAsync(CopiasDePrueba.Copia(CodigosDeValorNacional.Uvr, string.Empty, Hoy.AddDays(3), 407.9m, _c.D.G.UtcNow, hasta: Hoy.AddDays(3),
            unidad: "Pesos"))).Should().BeSameAs(EfectoAplicado.Nada, "una UVR nueva no corrige nada");

        var correccion = CorreccionDeUvr(Hoy.AddDays(3));
        var efecto = await ReplicarAsync(correccion);

        efecto.Conteos[EfectoDeReplicacionCorreccionDeValorNacional.ConteoCorridas].Should().Be(1);
        var corrida = await _c.Db.LendingProcessRuns.SingleAsync(r => r.Kind == LendingProcessKind.NationalValueCorrection);
        corrida.Should().BeEquivalentTo(new
        {
            Status = LendingProcessRunStatus.Requested, AffectedFrom = (DateOnly?)Hoy.AddDays(3), AffectedTo = (DateOnly?)Causado,
            RequestedByKind = ActorKind.Process, ExecutionOrigin = $"Replica:{correccion.SourcePublicId:D}",
        }, "la UVR de un día cambia también la corrección monetaria del día siguiente y, si ese día hubo un pago, el capital en UVR y lo que sigue: "
           + "la corrida llega hasta la marca (decisión del dueño del 2026-10-17; antes llegaba sólo al día corregido y el día siguiente quedaba descuadrado)");
    }

    [Fact]
    public async Task La_corrida_de_una_UVR_no_recorre_los_creditos_en_pesos()
    {
        var correccion = CorreccionDeUvr(Hoy.AddDays(3));
        await CopiasDePrueba.SembrarAsync(_c.Db, _c.D.G.UtcNow, correccion);
        _c.Db.ChangeTracker.Clear();
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.NationalValueCorrection, Status = LendingProcessRunStatus.Requested, AffectedFrom = Hoy.AddDays(3),
            AffectedTo = Hoy.AddDays(3), RequestedByKind = ActorKind.Process, RequestedAt = _c.D.G.UtcNow,
            ExecutionOrigin = EfectoDeReplicacionCorreccionDeValorNacional.PrefijoDeOrigen + correccion.SourcePublicId.ToString("D"),
        };
        _c.Db.LendingProcessRuns.Add(corrida);
        await _c.Db.SaveChangesAsync();

        var r = await _c.Corregir().Handle(new RunNationalValueCorrectionCommand(corrida.PublicId), default);

        r.Value.Should().Be(AvanceDeCorrida.Terminada);
        _c.Db.ChangeTracker.Clear();
        var leida = await _c.Db.LendingProcessRuns.SingleAsync(x => x.Id == corrida.Id);
        (leida.Status, leida.TotalLoans, leida.ProcessedLoans).Should().Be((LendingProcessRunStatus.Completed, 0, 0),
            "el crédito de Ana es en pesos y la UVR no lo toca");
        (await _c.Db.LoanTransactions.CountAsync(t => t.ProcessRunId == corrida.Id)).Should().Be(0);
    }

    // -------------------------------------------- K6b: el pago aplicado en un día cuya UVR se corrige (decisión del dueño, 2026-10-17) --

    [Fact]
    public void La_diferencia_del_reparto_de_un_pago_se_registra_como_reaplicacion_de_la_corrida()
    {
        var d = new IngenIA365ERP.Domain.Lending.Replay.DiferenciaDeLibro(Hoy.AddDays(3), LoanConcept.Principal, 1, -293_147.07m, -292_000.00m)
            { DePago = true };
        var mensaje = Guid.NewGuid();

        var fila = IngenIA365ERP.Application.Lending.Ledger.RegistroDeCartera.DiferenciaDeReparto(7, d, 9, Causado, _c.D.G.UtcNow, mensaje);

        (fila.Kind, fila.Concept, fila.InstallmentNumber, fila.Amount, fila.ProcessRunId, fila.OperationId, fila.MessagePublicId, fila.ValueDate,
                fila.AccountingDate)
            .Should().Be((LoanTransactionKind.PaymentReallocation, LoanConcept.Principal, (int?)1, 1_147.07m, (int?)9, (int?)null, (Guid?)mensaje,
                Hoy.AddDays(3), Causado), "el pago y sus filas no se tocan: la diferencia es una fila nueva de la corrida, fechada en el período abierto");
        var invalida = () => IngenIA365ERP.Application.Lending.Ledger.RegistroDeCartera.DiferenciaDeReparto(7, d with { DePago = false }, 9, Causado,
            _c.D.G.UtcNow, mensaje);
        invalida.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task El_reparto_de_un_pago_corregido_sale_en_su_propio_hecho_masivo_con_el_valor_corregido_y_el_dia_del_pago()
    {
        await _c.SembrarTiposDeHechoAsync();
        _c.Ir(Causado.AddDays(1));
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.NationalValueCorrection, Status = LendingProcessRunStatus.Running, AffectedFrom = Hoy.AddDays(3), AffectedTo = Causado,
            RequestedByKind = ActorKind.Process, RequestedAt = _c.D.G.UtcNow, ExecutionOrigin = EfectoDeReplicacionCorreccionDeValorNacional.PrefijoDeOrigen + Guid.NewGuid(),
        };
        _c.Db.LendingProcessRuns.Add(corrida);
        await _c.Db.SaveChangesAsync();
        var cargado = (await _c.Cargador().CargarAsync(_c.Credito.Id, null, Causado, default))!;
        var datos = (await _c.Partes().DatosAsync([cargado], default))[_c.Credito.Id];
        var dia = Hoy.AddDays(3);
        var filas = new List<IngenIA365ERP.Domain.Lending.Replay.MovimientoDeLibro>
        {
            new(dia, LoanTransactionKind.PaymentReallocation, LoanConcept.CurrentInterest, 1, -1_234.56m),
            new(dia, LoanTransactionKind.PaymentReallocation, LoanConcept.Principal, 1, 1_234.56m),
        };
        var corregido = new CorrectedNationalValueV1 { Code = CodigosDeValorNacional.Uvr, Key = string.Empty, ValidFrom = dia, SourcePublicId = Guid.NewGuid(), SourceSequence = 7 };
        var origen = new OrigenDePartes(AccountingFactPartSource.NationalValueCorrectionBatch, corrida, null, new Dictionary<DateOnly, int> { [Causado] = 1 }, null)
            { FechaDeRegistro = Causado.AddDays(1) };

        var r = await _c.Partes().MasivasAsync(origen, [new LineaDeCausacion(datos, dia, filas)], PagoRedistribuidoPorValorNacionalV1.Type, corregido);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        await _c.Db.SaveChangesAsync();

        var parte = r.Value.Partes.Should().ContainSingle().Subject;
        (parte.FactType, parte.TotalAmount, parte.ValueDate).Should().Be((PagoRedistribuidoPorValorNacionalV1.Type, 0m, Causado),
            "el reparto sólo redistribuye: suma cero");
        r.Value.Mensaje(_c.Credito.Id, dia, LoanConcept.Principal).Should().Be(parte.MessagePublicId);
        var mensaje = await _c.Db.IntegrationMessages.SingleAsync(m => m.PublicId == parte.MessagePublicId);
        mensaje.Type.Should().Be(PagoRedistribuidoPorValorNacionalV1.Type);
        mensaje.OriginEventKey.Should().Be($"Confirmation:{parte.PublicId:N}");
        mensaje.PayloadJson.Should().Contain("\"correctedNationalValue\":{").And.Contain("\"code\":\"UVR\"")
            .And.Contain($"\"valueDate\":\"{dia:yyyy-MM-dd}\"").And.Contain("\"runKind\":\"NationalValueCorrection\"")
            .And.Contain("\"concept\":\"Principal\"").And.Contain("\"concept\":\"CurrentInterest\"");
        (await _c.Db.IntegrationMessageDeliveries.SingleAsync(x => x.MessageId == mensaje.Id)).Mode.Should().Be(DeliveryMode.Batch,
            "por lotes, con el ajuste de la corrida");
    }

    [Fact]
    public void La_diferencia_de_la_correccion_monetaria_se_registra_con_su_clase_y_cero_en_UVR()
    {
        var d = new IngenIA365ERP.Domain.Lending.Replay.DiferenciaDeLibro(Hoy.AddDays(3), LoanConcept.Principal, null, 1_000m, 1_250m)
            { DeCorreccionMonetaria = true };
        var mensaje = Guid.NewGuid();

        var fila = IngenIA365ERP.Application.Lending.Ledger.RegistroDeCartera.DiferenciaDeCorreccionMonetaria(7, d, 9, Causado, _c.D.G.UtcNow, mensaje);

        (fila.Kind, fila.Concept, fila.Amount, fila.UvrAmount, fila.ProcessRunId, fila.MessagePublicId, fila.ValueDate)
            .Should().Be((LoanTransactionKind.MonetaryCorrection, LoanConcept.Principal, 250m, (decimal?)0m, (int?)9, (Guid?)mensaje, Hoy.AddDays(3)));
        var invalida = () => IngenIA365ERP.Application.Lending.Ledger.RegistroDeCartera.DiferenciaDeCorreccionMonetaria(7, d with { DeCorreccionMonetaria = false },
            9, Causado, _c.D.G.UtcNow, mensaje);
        invalida.Should().Throw<InvalidOperationException>();
    }
}
