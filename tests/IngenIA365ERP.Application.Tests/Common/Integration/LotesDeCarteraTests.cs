using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// Feature 013, T0624 (T0645, T15; contracts/contabilidad.md §6 y §10.2): las órdenes de lote con hechos de Cartera. El lote manual pedido con
/// <c>OriginModule = CAR</c> sólo toma entregas de Cartera y choca sólo con un lote de Cartera en curso
/// (<c>Accounting.LendingBatch.AlreadyRunning</c>); el de Inventario sigue igual. El reproceso de un hecho de Cartera conserva su fecha contable.
/// Los lotes de la causación (<c>LendingAccrualRun</c>) y del cierre (<c>LendingMonthlyClose</c>) los crea Cartera en su guardado (T0654, fase 12).
/// </summary>
public class LotesDeCarteraTests
{
    private readonly EscenarioContableDeCartera E = new();
    private readonly ISenalDeMensajes _senal = Substitute.For<ISenalDeMensajes>();

    private OrderIntegrationBatchCommandHandler Orden() => new(E.D.Db, E.ActorActual, E.D.Clock, _senal);

    private static OrderIntegrationBatchCommand Ordenar(Guid corte, string modulo) =>
        new(corte, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), null, null, null, "Cierre de noviembre")
        {
            OperationKey = Guid.NewGuid(),
            OriginModule = modulo,
        };

    private (Guid UltimoDeCartera, Guid DeInventario) Mezcla()
    {
        var cartera = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("03-causacion-del-dia"));
        var inventario = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("25-mora-causada"))
            .Select(m => m with { Envelope = m.Envelope with { OriginModule = ModulosDeOrigen.Inventario } }).ToList();
        var otraDeCartera = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("26-seguro-causado-con-la-aseguradora"));
        E.Emitir(cartera, DeliveryStatus.InBatch);
        E.Emitir(inventario, DeliveryStatus.InBatch);
        E.Emitir(otraDeCartera, DeliveryStatus.InBatch);
        return (otraDeCartera[0].MessageId, inventario[0].MessageId);
    }

    [Fact]
    public async Task El_lote_manual_de_Cartera_solo_toma_entregas_de_Cartera()
    {
        var (corte, deInventario) = Mezcla();

        var r = await Orden().Handle(Ordenar(corte, ModulosDeOrigen.Cartera), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Messages.Should().Be(2);
        E.D.Db.ChangeTracker.Clear();
        var lote = await E.D.Db.IntegrationBatches.SingleAsync();
        (await E.D.Db.IntegrationMessageDeliveries.Where(d => d.BatchId == lote.Id).Select(d => d.Message!.OriginModule).ToListAsync())
            .Should().OnlyContain(m => m == ModulosDeOrigen.Cartera);
        (await E.D.Db.IntegrationMessageDeliveries.SingleAsync(d => d.Message!.PublicId == deInventario)).BatchId.Should().BeNull("un lote nunca mezcla módulos");
    }

    [Fact]
    public async Task El_lote_manual_de_Inventario_no_toma_lo_de_Cartera()
    {
        var (_, deInventario) = Mezcla();

        var r = await Orden().Handle(Ordenar(deInventario, ModulosDeOrigen.Inventario), default);

        r.Value.Messages.Should().Be(1);
    }

    [Fact]
    public async Task Con_un_lote_de_Cartera_en_curso_responde_LendingBatch_AlreadyRunning()
    {
        var (corte, _) = Mezcla();
        var primero = await Orden().Handle(Ordenar(corte, ModulosDeOrigen.Cartera), default);
        var nuevo = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("12-causacion-en-suspenso"));
        E.Emitir(nuevo, DeliveryStatus.InBatch);

        var r = await Orden().Handle(Ordenar(nuevo[0].MessageId, ModulosDeOrigen.Cartera), default);

        r.Error.Code.Should().Be("Accounting.LendingBatch.AlreadyRunning");
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { batchPublicId = primero.Value.BatchPublicId, number = primero.Value.Number });
    }

    [Fact]
    public async Task Un_lote_de_Cartera_en_curso_no_detiene_al_de_Inventario()
    {
        var (corte, deInventario) = Mezcla();
        (await Orden().Handle(Ordenar(corte, ModulosDeOrigen.Cartera), default)).IsSuccess.Should().BeTrue();

        var r = await Orden().Handle(Ordenar(deInventario, ModulosDeOrigen.Inventario), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
    }

    [Fact]
    public void El_lote_manual_es_de_Inventario_o_de_Cartera()
    {
        var v = new OrderIntegrationBatchCommandValidator();
        v.Validate(Ordenar(Guid.NewGuid(), ModulosDeOrigen.Cartera)).IsValid.Should().BeTrue();
        v.Validate(Ordenar(Guid.NewGuid(), ModulosDeOrigen.Tesoreria)).IsValid.Should().BeFalse();
        new OrderIntegrationBatchCommand(Guid.NewGuid(), default, default, null, null, null, "x").OriginModule
            .Should().Be(ModulosDeOrigen.Inventario, "la ruta de Inventario no cambia");
    }

    [Fact]
    public async Task El_reproceso_de_un_hecho_de_Cartera_conserva_su_fecha_contable()
    {
        var rechazado = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("01-desembolso-por-transferencia"));
        E.Emitir(rechazado, DeliveryStatus.Rejected);
        var handler = new ReprocessMessagesCommandHandler(E.D.Db, E.ActorActual, E.D.Clock, [new DestinoFalso(IntegrationDestinations.Accounting)], _senal);

        var r = await handler.Handle(new ReprocessMessagesCommand([rechazado[0].MessageId], "Se creó la regla de capital B") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Trigger.Should().Be(BatchTrigger.Reprocess);
        E.D.Db.ChangeTracker.Clear();
        var entrega = await E.D.Db.IntegrationMessageDeliveries.Include(d => d.Message).SingleAsync();
        entrega.Status.Should().Be(DeliveryStatus.InBatch);
        entrega.Message!.OperationDate.Should().Be(new DateOnly(2026, 11, 3), "el hecho conserva su fecha contable original");
    }
}
