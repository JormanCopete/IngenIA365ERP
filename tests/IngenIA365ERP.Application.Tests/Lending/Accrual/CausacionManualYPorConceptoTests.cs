using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Periods;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Accrual;

/// <summary>
/// Feature 024, fase 1 (pedido del dueño del 2026-11-16) sobre el ciclo diario real (el crédito de Ana, 3.000.000 al 20 % EA desde el 2026-12-01, con
/// los tipos de hecho sembrados):
/// <list type="bullet">
/// <item>con <c>Causacion.ModoInteresCorriente = Manual</c>, el interés corriente se causa igual (libro y motor no cambian) pero su hecho queda
/// esperando con el disparador <c>CausacionManual</c> y el alcance <c>CausacionManual:{d}</c>: el lote del fin de la causación no lo toma, el
/// programador de lotes tampoco, lo informa <see cref="CausacionPorContabilizar"/>, lo toma un lote manual de Cartera y el cierre contable del mes lo
/// cuenta como pendiente;</item>
/// <item>con <c>Contabilidad.CausacionSeparadaPorConcepto = Sí</c>, la parte del interés lleva la marca por concepto en su granularidad.</item>
/// <item>la tasa general de mora (<c>Mora.TasaGeneral</c>) llega por el cargador a la entrada de la reconstrucción y rige desde su vigencia;
/// con una R anterior a su registro no existe.</item>
/// </list>
/// </summary>
public class CausacionManualYPorConceptoTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;

    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    public async Task InitializeAsync()
    {
        _c = await CausacionDePrueba.CrearAsync();
        await _c.SembrarTiposDeHechoAsync();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private async Task VigenciaAsync(string clave, string valor)
    {
        _c.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedBy = "test",
        });
        await _c.Db.SaveChangesAsync();
    }

    private async Task<IntegrationMessageDeliveryConMensaje> EntregaDelInteresAsync()
    {
        var mensaje = await _c.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == InteresCausadoV1.Type);
        var entrega = await _c.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(x => x.MessageId == mensaje.Id);
        return new(mensaje.PublicId, entrega.Id, entrega.Mode, entrega.Status, entrega.ScheduleKey, entrega.BatchScopeKey, entrega.BatchId);
    }

    private sealed record IntegrationMessageDeliveryConMensaje(Guid Mensaje, int Id, DeliveryMode Mode, DeliveryStatus Status, string? ScheduleKey,
        string? BatchScopeKey, int? BatchId);

    [Fact]
    public async Task En_modo_manual_lo_causado_queda_esperando_hasta_que_lo_tome_un_lote_manual()
    {
        await VigenciaAsync(ParametrosDeCartera.CausacionModoInteresCorriente, ParametrosDeCartera.CausacionManual);
        var d = Hoy.AddDays(1);

        await _c.CausarHastaAsync(d);

        (await _c.Db.LoanTransactions.AnyAsync(t => t.ValueDate == d)).Should().BeTrue("el motor y el libro causan igual");
        var entrega = await EntregaDelInteresAsync();
        entrega.Mode.Should().Be(DeliveryMode.Batch);
        entrega.Status.Should().Be(DeliveryStatus.InBatch);
        entrega.ScheduleKey.Should().Be("CAR.ICA|CausacionManual||PorDia");
        entrega.BatchScopeKey.Should().Be(ModoDePasoDeCartera.AlcanceDeCausacionManual(d));
        entrega.BatchId.Should().BeNull("el lote del fin de la causación sólo toma AccrualRun:{x ≤ d}");

        // El programador de lotes no lo toma: no es una franja diaria.
        var programados = await new ScheduleIntegrationBatchesCommandHandler(_c.Db, _c.ActorDelProceso, _c.D.G.Reloj, new LectorDeParametros(_c.Db))
            .Handle(new ScheduleIntegrationBatchesCommand(0), default);
        programados.Value.Created.Should().BeEmpty();

        // El punto de la fase 3: lo pendiente por día y concepto.
        var pendientes = new CausacionPorContabilizar(_c.Db);
        var resumen = await pendientes.ResumenAsync(Hoy, d, [InteresCausadoV1.Type], default);
        resumen.Should().ContainSingle().Which.Should().Match<CausacionPendienteDeContabilizar>(r =>
            r.Dia == d && r.Hecho == InteresCausadoV1.Type && r.Hechos == 1 && r.Creditos == 1 && r.Total > 0m);
        (await pendientes.ResumenAsync(Hoy, d, [MoraCausadaV1.Type], default)).Should().BeEmpty();
        (await pendientes.Entregas(d.AddDays(1), d.AddDays(5)).CountAsync()).Should().Be(0, "el rango es de días causados");

        // El cierre contable del mes lo cuenta como pendiente (Accounting.Period.LendingPending).
        var cierre = await CarteraEnElCierre.EvaluarAsync(_c.Db, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), default);
        cierre.Pendientes.InBatch.Should().BeGreaterThanOrEqualTo(1);
        cierre.Pendientes.Types.Should().Contain(InteresCausadoV1.Type);

        // Mientras no exista el proceso de causación, el lote manual de Cartera lo toma como a cualquier entrega sin lote de su rango.
        var alcance = new AlcanceDeLote(d, d, null, null, null, ModulosDeOrigen.Cartera);
        (await alcance.Entregas(_c.Db).Select(x => x.Id).ToListAsync()).Should().Contain(entrega.Id);
        var lote = await new OrderIntegrationBatchCommandHandler(_c.Db, _c.ActorDelProceso, _c.D.G.Reloj)
            .Handle(new OrderIntegrationBatchCommand(entrega.Mensaje, d, d, null, null, null, "Contabilizar la causación manual")
            {
                OperationKey = Guid.NewGuid(), OriginModule = ModulosDeOrigen.Cartera,
            }, default);
        lote.IsSuccess.Should().BeTrue(lote.IsFailure ? lote.Error.Message : null);
        _c.Db.ChangeTracker.Clear();
        (await EntregaDelInteresAsync()).BatchId.Should().NotBeNull();
        (await pendientes.ResumenAsync(Hoy, d, null, default)).Should().BeEmpty("ya está en el lote manual");
    }

    [Fact]
    public async Task La_tasa_general_de_mora_llega_a_la_reconstruccion_y_rige_desde_su_vigencia()
    {
        // Mora del crédito: 1,5 × 20 % = 30 % EA (topada en la usura del 25 %). La empresa registra, una hora después del desembolso, una general de
        // 18 % desde el 05-01-2027.
        _c.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.MoraTasaGeneral, ScopeKind = ParameterScopeKind.None, Value = "0.18",
            ValidFrom = new DateOnly(2027, 1, 5), Reason = "prueba", CreatedBy = "test", CreatedAt = _c.D.G.UtcNow.AddHours(1),
        });
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();

        var cargado = await _c.Cargador().CargarAsync(_c.Credito.Id, null, new DateOnly(2027, 1, 31), default);

        cargado!.Entrada!.MoraGeneral.Should().ContainSingle().Which.Should().Match<Domain.Lending.Replay.TasaGeneralDeMora>(t =>
            t.ValidFrom == new DateOnly(2027, 1, 5) && t.Ea == 0.18m);
        var antes = MotorDeCartera.Reconstruir(cargado.Entrada, new DateOnly(2027, 1, 4)).TasaDelDia!;
        antes.MoraEA.Should().Be(0.30m, "antes de su vigencia, la del crédito");
        antes.MoraTopada.Should().BeTrue();
        var despues = MotorDeCartera.Reconstruir(cargado.Entrada, new DateOnly(2027, 1, 10)).TasaDelDia!;
        despues.MoraEA.Should().Be(0.18m, "desde su vigencia, la general de la empresa, también para un crédito ya desembolsado");
        despues.MoraTopada.Should().BeFalse("bajo la usura no se topa");

        var conRAnterior = await _c.Cargador().CargarAsync(_c.Credito.Id, _c.D.G.UtcNow.AddMinutes(30), new DateOnly(2027, 1, 31), default);
        conRAnterior!.Entrada!.MoraGeneral.Should().BeEmpty("con lo que se sabía antes del registro no había tasa general");
    }

    [Fact]
    public async Task En_modo_automatico_todo_sigue_como_siempre()
    {
        var d = Hoy.AddDays(1);

        await _c.CausarHastaAsync(d);

        var entrega = await EntregaDelInteresAsync();
        entrega.ScheduleKey.Should().Be("CAR.ICA|FinDeCausacion||PorDia", "de fábrica nada cambia");
        entrega.BatchScopeKey.Should().Be(EmisionDeCartera.AlcanceDeLoteDeCausacion(d));
        entrega.BatchId.Should().NotBeNull();
        (await new CausacionPorContabilizar(_c.Db).ResumenAsync(Hoy, d, null, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Con_la_causacion_separada_por_concepto_la_parte_lleva_la_marca_y_sigue_en_el_lote_del_dia()
    {
        await VigenciaAsync(ParametrosDeCartera.ContabilidadCausacionSeparadaPorConcepto, "true");
        var d = Hoy.AddDays(1);

        await _c.CausarHastaAsync(d);

        var entrega = await EntregaDelInteresAsync();
        entrega.ScheduleKey.Should().Be("CAR.ICA|FinDeCausacion||PorDia" + ClavesDeLote.MarcaPorConcepto);
        entrega.BatchScopeKey.Should().Be(EmisionDeCartera.AlcanceDeLoteDeCausacion(d));
        entrega.BatchId.Should().NotBeNull("separar por concepto cambia los comprobantes, no cuándo pasa");
    }
}
