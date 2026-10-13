using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Integration;
using IngenIA365ERP.Application.Accounting.Inventory.Consultas;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Lotes;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Integration;

/// <summary>
/// Feature 013, T0618 (T0644, T0645, T15; contracts/contabilidad.md §6): <c>DestinoContabilidad</c>, ya en la carpeta común, sigue siendo el
/// único consumidor del destino <c>Accounting</c> y reparte por módulo de origen. <c>Acepta</c> responde por el módulo del tipo en el catálogo
/// agregado; <c>ConsumirAsync</c> manda lo de Inventario a sus comandos sin cambios y lo de Cartera a los suyos; <c>PlanearLote</c> nunca mezcla
/// módulos; <c>TotalesDelLoteAsync</c> suma los dos recibos; el respaldo de horario de Inventario sólo vale para sus claves y el conteo de
/// pendientes es por módulo.
/// </summary>
public class DestinoContabilidadConDosEmisoresTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    private DestinoContabilidad Destino() => new(_sender, NullLogger<DestinoContabilidad>.Instance);

    private static UnidadDeConsumo Unidad(string modulo, Guid? lote = null) =>
        new(IntegrationDestinations.Accounting, Guid.NewGuid(), "Confirmation", [Guid.NewGuid()], lote, 0) { OriginModule = modulo };

    [Fact]
    public void Acepta_busca_el_tipo_en_el_catalogo_agregado_y_responde_por_su_modulo()
    {
        var destino = Destino();

        destino.Destino.Should().Be(IntegrationDestinations.Accounting);
        destino.Acepta(CompraRecibidaV1.Type, 1).Should().BeTrue("de Inventario");
        destino.Acepta(InteresCausadoV1.Type, 1).Should().BeTrue("de Cartera");
        destino.Acepta(CarteraActivadaV1.Type, 1).Should().BeTrue("un informativo de Cartera también se recibe");
        destino.Acepta(InteresCausadoV1.Type, 2).Should().BeFalse();
        destino.Acepta(VentaACreditoRegistradaV1.Type, 1).Should().BeFalse("va a Cartera, no a Contabilidad");
        VersionesAceptadas.Todas.Should().HaveCount(18, "las de Inventario no cambian");
        VersionesAceptadas.DeCartera.Should().HaveCount(35);
        VersionesAceptadas.Acepta(ModulosDeOrigen.Inventario, InteresCausadoV1.Type, 1).Should().BeFalse("por módulo");
    }

    [Fact]
    public async Task Reparte_cada_unidad_por_su_modulo_de_origen()
    {
        _sender.Send(Arg.Any<PostInventoryMessagesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(Guid.NewGuid(), "EI", "1")));
        _sender.Send(Arg.Any<PostLendingFactsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(Guid.NewGuid(), "RC", "1")));
        var inventario = Unidad(ModulosDeOrigen.Inventario);
        var cartera = Unidad(ModulosDeOrigen.Cartera);

        var deInventario = await Destino().ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(inventario), default);
        var deCartera = await Destino().ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(cartera), default);

        ((ResultadoDeConsumo.Processed)deInventario.Single().Resultado).VoucherTypeCode.Should().Be("EI");
        ((ResultadoDeConsumo.Processed)deCartera.Single().Resultado).VoucherTypeCode.Should().Be("RC");
        await _sender.Received(1).Send(Arg.Is<PostInventoryMessagesCommand>(c => c.MessagePublicIds.SequenceEqual(inventario.MessagePublicIds)), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<PostLendingFactsCommand>(c => c.MessagePublicIds.SequenceEqual(cartera.MessagePublicIds)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_grupo_resumido_va_al_comando_resumido_de_su_modulo()
    {
        var lote = Guid.NewGuid();
        _sender.Send(Arg.Any<PostLendingSummaryGroupCommand>(), Arg.Any<CancellationToken>())
            .Returns(c => Result.Success<IReadOnlyList<ResultadoDeUnidad>>([]));
        _sender.Send(Arg.Any<PostInventorySummaryGroupCommand>(), Arg.Any<CancellationToken>())
            .Returns(c => Result.Success<IReadOnlyList<ResultadoDeUnidad>>([]));

        await Destino().ConsumirAsync(new TrabajoDeConsumo("2026-11-14|CA", [Unidad(ModulosDeOrigen.Cartera, lote), Unidad(ModulosDeOrigen.Cartera, lote)]), default);
        await Destino().ConsumirAsync(new TrabajoDeConsumo("2026-03-14|VTA", [Unidad(ModulosDeOrigen.Inventario, lote)]), default);

        await _sender.Received(1).Send(Arg.Is<PostLendingSummaryGroupCommand>(c => c.GroupKey == "2026-11-14|CA" && c.BatchPublicId == lote && c.MessagePublicIds.Count == 2),
            Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<PostInventorySummaryGroupCommand>(c => c.GroupKey == "2026-03-14|VTA"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_excepcion_es_reintento_y_un_fallo_de_validacion_es_rechazo()
    {
        _sender.Send(Arg.Any<PostLendingFactsCommand>(), Arg.Any<CancellationToken>()).Returns<Result<ResultadoDeConsumo>>(_ => throw new InvalidOperationException("base caída"));
        (await Destino().ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(Unidad(ModulosDeOrigen.Cartera)), default)).Single().Resultado
            .Should().BeOfType<ResultadoDeConsumo.Retry>();

        _sender.Send(Arg.Any<PostLendingFactsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ResultadoDeConsumo>(new Error("Validation.Failed", "La unidad no trae hechos.")));
        (await Destino().ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(Unidad(ModulosDeOrigen.Cartera)), default)).Single().Resultado
            .Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Validation.Failed");
    }

    [Fact]
    public void PlanearLote_nunca_mezcla_modulos()
    {
        var cartera = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("03-causacion-del-dia"))
            .Select(m => m with { Entrega = m.Entrega with { Status = DeliveryStatus.InBatch, ScheduleKey = EscenarioContableDeCartera.Horario(m.Type) } })
            .ToList();
        var otraParte = EscenarioContableDeCartera.Copia(cartera);
        var deInventario = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("25-mora-causada"))
            .Select(m => m with { Envelope = m.Envelope with { OriginModule = ModulosDeOrigen.Inventario }, Entrega = m.Entrega with { ScheduleKey = null } })
            .ToList();

        var trabajos = Destino().PlanearLote([.. cartera, .. deInventario, .. otraParte]);

        trabajos.Should().OnlyContain(t => t.Unidades.Select(u => u.OriginModule).Distinct().Count() == 1);
        trabajos.Should().ContainSingle(t => t.ClaveDeGrupo == "2026-11-14|CA").Which.Unidades.Should().HaveCount(2);
        trabajos.Should().ContainSingle(t => t.Unidades.All(u => u.OriginModule == ModulosDeOrigen.Inventario));
    }

    [Fact]
    public async Task Los_totales_del_lote_suman_los_recibos_de_los_dos_modulos()
    {
        var lote = Guid.NewGuid();
        _sender.Send(Arg.Is<TotalesDeLoteDeInventarioQuery>(q => q.BatchPublicId == lote), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TotalesDeLoteEnDestino(100m, 100m)));
        _sender.Send(Arg.Is<GetLendingBatchTotalsQuery>(q => q.BatchPublicId == lote), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new TotalesDeLoteEnDestino(6452.48m, 6452.48m)));

        (await Destino().TotalesDelLoteAsync(lote, default)).Should().Be(new TotalesDeLoteEnDestino(6552.48m, 6552.48m));
    }

    [Fact]
    public void El_respaldo_de_horario_de_Inventario_solo_vale_para_sus_claves()
    {
        var cartera = ClavesDeLote.Leer("CAR.PAP|HoraDiaria||PorHecho")!;
        cartera.Modulo.Should().Be(ModulosDeOrigen.Cartera);
        cartera.EsDeInventario.Should().BeFalse("una clave de Cartera sella su hora o no es de franja diaria (§10.2)");
        ClavesDeLote.Leer("COMPRA|HoraDiaria||PorDocumento")!.EsDeInventario.Should().BeTrue();
    }

    [Fact]
    public async Task Los_pendientes_se_cuentan_por_modulo()
    {
        var e = new EscenarioContableDeCartera();
        e.Emitir(EscenarioContableDeCartera.UnidadDe("03-causacion-del-dia"));
        e.Emitir(EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("25-mora-causada")), DeliveryStatus.Rejected);
        e.Emitir(EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("26-seguro-causado-con-la-aseguradora"))
            .Select(m => m with { Envelope = m.Envelope with { OriginModule = ModulosDeOrigen.Inventario } }).ToList());
        var noviembre = (new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        var cartera = await new ListPendingIntegrationMessagesQueryHandler(e.D.Db)
            .Handle(new ListPendingIntegrationMessagesQuery(ModulosDeOrigen.Cartera, noviembre.Item1, noviembre.Item2), default);
        var inventario = await new PendingInventoryMessagesQueryHandler(e.D.Db).Handle(new PendingInventoryMessagesQuery(noviembre.Item1, noviembre.Item2), default);

        cartera.Value.Pending.Should().Be(1);
        cartera.Value.Rejected.Should().Be(1);
        inventario.Value.Total.Should().Be(1, "PendingInventoryMessagesQuery sigue contando sólo INV");
    }
}
