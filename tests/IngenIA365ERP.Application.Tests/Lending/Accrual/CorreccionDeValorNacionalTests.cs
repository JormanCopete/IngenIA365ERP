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
}
